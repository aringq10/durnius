namespace Durnius.Web.Features.Multiplayer;

/// <summary>
/// A SignalR operation requested by a manager. Managers only describe the
/// effect; the hub performs it after coordination gates have been released.
/// </summary>
public abstract record CoordinationEffect;

public sealed record AddToGroupEffect(string ConnectionId, string Group) : CoordinationEffect;

public sealed record RemoveFromGroupEffect(string ConnectionId, string Group) : CoordinationEffect;

public sealed record LobbyUpdateEffect(string LobbyName, LobbyUpdateDto Update) : CoordinationEffect;

public sealed record ResponseEffect(Guid MatchId, Response Response) : CoordinationEffect;

public sealed class CoordinationEffects
{
    private readonly List<CoordinationEffect> _items = [];

    public IReadOnlyList<CoordinationEffect> Items => _items;

    public void Add(CoordinationEffect effect) => _items.Add(effect);

    public void AddToGroup(string connectionId, string group) =>
        _items.Add(new AddToGroupEffect(connectionId, group));

    public void RemoveFromGroup(string connectionId, string group) =>
        _items.Add(new RemoveFromGroupEffect(connectionId, group));

    public void LobbyUpdated(LobbyState state) =>
        _items.Add(new LobbyUpdateEffect(state.Name, new LobbyUpdateDto(state)));

    public void SendResponse(Guid matchId, Response response) =>
        _items.Add(new ResponseEffect(matchId, response));
}
