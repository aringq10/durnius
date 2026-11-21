namespace Durnius.Web.Features.Multiplayer;

/// <summary>
/// A coordination scope. All room-local state is mutated under <see cref="Gate"/>.
/// </summary>
public abstract class Room
{
    internal SemaphoreSlim Gate { get; } = new(1, 1);
}

/// <summary>
/// A waiting room for one game type. Reset to empty whenever a match starts.
/// </summary>
public sealed class LobbyRoom(string name, int capacity) : Room
{
    private readonly List<Session> _members = [];

    public string Name { get; } = name;
    public int Capacity { get; } = capacity;

    // Guarded by Gate.
    internal IReadOnlyList<Session> Members => _members;
    internal bool IsFull => _members.Count >= Capacity;

    internal void Add(Session session) => _members.Add(session);
    internal bool Remove(Session session) => _members.Remove(session);
    internal void Clear() => _members.Clear();

    internal LobbyState State =>
        new(Name, Capacity, _members.Count, _members.Select(m => new MatchPlayer(m.ConnectionId, m.Username)).ToList());
}

/// <summary>
/// An active match. Ownership of all players is published atomically with
/// registration, and the room is finalized at most once.
/// </summary>
public sealed class MatchRoom : Room
{
    public Guid Id { get; } = Guid.NewGuid();
    public IMatch Match { get; }
    public IReadOnlyList<Session> Players { get; }

    // Guarded by Gate.
    internal bool Finalized { get; set; }

    internal MatchRoom(IMatch match, IEnumerable<Session> players)
    {
        Match = match;
        Players = players.ToList();
    }
}
