namespace Durnius.Web.Features.Multiplayer;

public record LobbyState(int Capacity, int Count, IReadOnlyList<MatchPlayer> Players);

public class LobbyManager
{
    private const int _capacity = 2;

    private readonly object _gate = new();
    private readonly Dictionary<string, string> _occupants = new();

    public LobbyState GetState()
    {
        return new(_capacity, _occupants.Count, GetPlayers());
    }

    public (LobbyState? State, bool StartMatch) Join(string connectionId, string username)
    {
        lock (_gate)
        {
            foreach (var occupant in _occupants)
            {
                if (occupant.Value == username && occupant.Key != connectionId)
                {
                    return (null, false);
                }
            }

            _occupants[connectionId] = username;

            var players = GetPlayers();

            bool startMatch = false;

            if (_occupants.Count == _capacity)
            {
                _occupants.Clear();
                startMatch = true;
            }

            return (new LobbyState
            (
                Capacity: _capacity,
                Count: players.Count,
                Players: players
            ), startMatch);
        }
    }

    public bool Remove(string connectionId)
    {
        lock (_gate)
        {
            return _occupants.Remove(connectionId);
        }
    }

    public IReadOnlyList<MatchPlayer> GetPlayers()
    {
        return _occupants
            .Select(occupant => new MatchPlayer(occupant.Key, occupant.Value))
            .ToList();
    }
}
