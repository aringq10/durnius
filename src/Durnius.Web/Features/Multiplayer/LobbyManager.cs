namespace Durnius.Web.Features.Multiplayer;

public record LobbyState(string Name, int Capacity, int Count, IReadOnlyList<MatchPlayer> Players);

public record LobbyUpdateDto
{
    public int Capacity { get; init; }
    public int Count { get; init; }
    public IReadOnlyList<string> Players { get; init; }

    public LobbyUpdateDto(LobbyState state)
    {
        Capacity = state.Capacity;
        Count = state.Count;
        Players = state.Players.Select(player => player.Username).ToList();
    }
}

public sealed record JoinResult(string? Rejection, CoordinationEffects? Effects)
{
    public static JoinResult Rejected(string reason) => new(reason, null);
    public static JoinResult Accepted(CoordinationEffects effects) => new(null, effects);
}

/// <summary>
/// Owns lobby rooms and the atomic lobby-to-match transition.
/// </summary>
/// <remarks>
/// Lock order: lobby gate, then session gate. During a transition the new
/// match gate and the remaining member session gates are acquired while still
/// holding the lobby gate; because room actions always take their room gate
/// before a session gate, no member can hold a session gate while waiting for
/// this lobby gate.
/// </remarks>
public sealed class LobbyManager
{
    private readonly MatchManager _matches;
    private readonly Dictionary<string, LobbyRoom> _lobbies = [];

    public LobbyManager(MatchRegistry matchRegistry, MatchManager matches)
    {
        _matches = matches;

        foreach (var (name, capacity) in matchRegistry.GetAll())
        {
            _lobbies[name] = new LobbyRoom(name, capacity);
        }
    }

    public async Task<JoinResult> JoinAsync(Session session, string lobbyName)
    {
        if (!_lobbies.TryGetValue(lobbyName, out var lobby))
        {
            return JoinResult.Rejected("Unknown lobby");
        }

        await lobby.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await session.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (session.Room is MatchRoom)
                {
                    return JoinResult.Rejected("Already in a match");
                }
                if (session.Room is not null)
                {
                    return JoinResult.Rejected("Already in another lobby");
                }
                if (lobby.Members.Any(m => m.Username == session.Username))
                {
                    return JoinResult.Rejected("Already in this lobby");
                }

                var players = lobby.Members.Append(session).ToList();
                var startsMatch = players.Count >= lobby.Capacity;

                // Create first so an invalid match definition cannot leave a
                // half-completed join behind.
                MatchRoom? room = null;
                Response? initialResponse = null;
                if (startsMatch)
                {
                    (room, initialResponse) = _matches.Create(lobby.Name, players);
                }

                lobby.Add(session);
                session.Room = lobby;

                var effects = new CoordinationEffects();
                effects.AddToGroup(session.ConnectionId, lobby.Name);
                effects.LobbyUpdated(lobby.State);

                if (startsMatch)
                {
                    await TransitionToMatchAsync(lobby, session, room!, initialResponse!, players, effects)
                        .ConfigureAwait(false);
                }

                return JoinResult.Accepted(effects);
            }
            finally
            {
                session.Gate.Release();
            }
        }
        finally
        {
            lobby.Gate.Release();
        }
    }

    public Task<CoordinationEffects?> LeaveAsync(Session session) =>
        session.RunInRoomAsync<LobbyRoom, CoordinationEffects>(lobby => LeaveInRoomAsync(session, lobby));

    // Caller holds lobby.Gate and session.Gate.
    internal Task<CoordinationEffects> LeaveInRoomAsync(Session session, LobbyRoom lobby)
    {
        lobby.Remove(session);
        session.Room = null;

        var effects = new CoordinationEffects();
        effects.RemoveFromGroup(session.ConnectionId, lobby.Name);
        effects.LobbyUpdated(lobby.State);
        return Task.FromResult(effects);
    }

    // Caller holds lobby.Gate and joiner.Gate.
    private async Task TransitionToMatchAsync(
        LobbyRoom lobby,
        Session joiner,
        MatchRoom room,
        Response initialResponse,
        IReadOnlyList<Session> players,
        CoordinationEffects effects)
    {
        await room.Gate.WaitAsync().ConfigureAwait(false);
        var held = new List<Session>();
        try
        {
            // Deterministic order; the joiner's gate is already held by the caller.
            foreach (var player in players.OrderBy(p => p.ConnectionId, StringComparer.Ordinal))
            {
                if (ReferenceEquals(player, joiner))
                {
                    continue;
                }

                await player.Gate.WaitAsync().ConfigureAwait(false);
                held.Add(player);
            }

            // Publish room ownership while nobody can discover the match
            // through a partially initialized registration.
            foreach (var player in players)
            {
                player.Room = room;
            }
            lobby.Clear();
            _matches.Publish(room);

            foreach (var player in players)
            {
                effects.RemoveFromGroup(player.ConnectionId, lobby.Name);
                effects.AddToGroup(player.ConnectionId, room.Id.ToString());
            }
            effects.SendResponse(room.Id, initialResponse);
        }
        finally
        {
            for (var i = held.Count - 1; i >= 0; i--)
            {
                held[i].Gate.Release();
            }
            room.Gate.Release();
        }
    }
}
