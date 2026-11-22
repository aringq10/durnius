using System.Collections.Concurrent;

namespace Durnius.Web.Features.Multiplayer;

/// <summary>
/// Owns match rooms, move/disconnect forwarding and end-of-match cleanup.
/// </summary>
/// <remarks>
/// Lock order: match gate, then session gates. Finalization takes the remaining
/// player gates under the match gate and is idempotent via
/// <see cref="MatchRoom.Finalized"/>.
/// </remarks>
public sealed class MatchManager(MatchRegistry registry)
{
    private readonly MatchRegistry _registry = registry;
    private readonly ConcurrentDictionary<string, MatchRoom> _byConnection = new();
    private readonly ConcurrentDictionary<Guid, MatchRoom> _active = new();

    public IReadOnlyCollection<Guid> ActiveMatchIds => _active.Keys.ToList();

    public Guid? GetMatchId(string connectionId) =>
        _byConnection.TryGetValue(connectionId, out var room) ? room.Id : null;

    // Creates a room without publishing it: no session ownership or map writes.
    internal (MatchRoom Room, Response Response) Create(string name, IReadOnlyList<Session> players)
    {
        var options = new MatchOptions(players.Select(p => new MatchPlayer(p.ConnectionId, p.Username)).ToList());
        var (match, response) = _registry.Create(name, options);
        return (new MatchRoom(match, players, name), response);
    }

    // Caller holds the new room's gate and every player's session gate.
    internal void Publish(MatchRoom room)
    {
        foreach (var player in room.Players)
        {
            _byConnection[player.ConnectionId] = room;
        }
        _active[room.Id] = room;
    }

    internal void Unpublish(MatchRoom room)
    {
        foreach (var player in room.Players)
        {
            _byConnection.TryRemove(new KeyValuePair<string, MatchRoom>(player.ConnectionId, room));
        }
        _active.TryRemove(room.Id, out _);
    }

    public Task<CoordinationEffects?> ForwardMoveAsync(Session session, object move) =>
        session.RunInRoomAsync<MatchRoom, CoordinationEffects>(
            room => HandleAsync(room, session, () => room.Match.MakeMove(session.ConnectionId, move)));

    // Caller already holds room.Gate and session.Gate (disconnect dispatch).
    internal Task<CoordinationEffects> DisconnectInRoomAsync(Session session, MatchRoom room) =>
        HandleAsync(room, session, () => room.Match.Disconnect(session.ConnectionId));

    private async Task<CoordinationEffects> HandleAsync(
        MatchRoom room,
        Session caller,
        Func<(MatchResult? Result, Response Response)> action)
    {
        if (room.Finalized)
        {
            return new CoordinationEffects();
        }

        var (result, response) = action();
        var effects = new CoordinationEffects();

        if (response is not null)
        {
            effects.SendResponse(room.Id, response);
        }

        if (result is not null)
        {
            effects.RecordMatch(room.Game, result);
            await FinalizeAsync(room, caller, effects).ConfigureAwait(false);
        }

        return effects;
    }

    private async Task FinalizeAsync(MatchRoom room, Session caller, CoordinationEffects effects)
    {
        var held = new List<Session>();
        try
        {
            foreach (var player in room.Players.OrderBy(p => p.ConnectionId, StringComparer.Ordinal))
            {
                if (ReferenceEquals(player, caller))
                {
                    continue;
                }

                await player.Gate.WaitAsync().ConfigureAwait(false);
                held.Add(player);
            }

            room.Finalized = true;
            foreach (var player in room.Players)
            {
                player.Room = null;
                effects.RemoveFromGroup(player.ConnectionId, room.Id.ToString());
            }
            Unpublish(room);
        }
        finally
        {
            for (var i = held.Count - 1; i >= 0; i--)
            {
                held[i].Gate.Release();
            }
        }
    }
}
