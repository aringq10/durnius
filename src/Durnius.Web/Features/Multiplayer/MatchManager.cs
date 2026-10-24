using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Durnius.Web.Features.Multiplayer;

public class MatchManager(IHubContext<LobbyHub> hubContext)
{
    private readonly ConcurrentDictionary<Guid, IMatch> _matches = [];
    private readonly ConcurrentDictionary<string, Guid> _matchByConnection = [];
    private readonly IHubContext<LobbyHub> _hubContext = hubContext;

    public async Task<(Guid Guid, Response Response)> StartNewMatch(Game name, MatchOptions options)
    {
        var r = MatchRegistry.GetMatch(name, options);

        var guid = Guid.NewGuid();

        _matches.TryAdd(guid, r.Match);
        foreach (var p in r.Match.Players)
        {
            SetByConnection(p.ConnectionId, guid);
            await _hubContext.Groups.AddToGroupAsync(p.ConnectionId, guid.ToString());
        }

        return (guid, r.Response);
    }

    public (Response? Response, bool Ended, IReadOnlyList<MatchPlayer>? Players) ForwardMove(Guid guid, string connectionId, object move)
    {
        if (!_matches.TryGetValue(guid, out var match))
        {
            return (null, false, null);
        }

        var result = match.MakeMove(connectionId, move);

        if (!match.IsRunning)
        {
            var players = match.Players;
            return (result, true, players);
        }

        return (result, false, null);
    }

    public (Response? Response, bool Ended, IReadOnlyList<MatchPlayer>? Players) ForwardsDisconnect(Guid guid, string connectionId)
    {
        if (!_matches.TryGetValue(guid, out var match))
        {
            return (null, false, null);
        }

        var result = match.Disconnect(connectionId);

        if (!match.IsRunning)
        {
            var players = match.Players;
            return (result, true, players);
        }

        return (result, false, null);
    }

    public async Task CleanupMatch(Guid guid, IReadOnlyList<MatchPlayer> players)
    {
        _matches.TryRemove(guid, out _);
        foreach (var p in players)
        {
            ClearByConnection(p.ConnectionId);
            await _hubContext.Groups.RemoveFromGroupAsync(p.ConnectionId, guid.ToString());
        }
    }

    public Guid? GetByConnection(string connectionId) =>
        _matchByConnection.TryGetValue(connectionId, out var id) ? id : null;

    private void SetByConnection(string connectionId, Guid matchId) => _matchByConnection[connectionId] = matchId;

    private void ClearByConnection(string connectionId) => _matchByConnection.TryRemove(connectionId, out _);
}
