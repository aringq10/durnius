namespace Durnius.Web.Features.Multiplayer;

public record LobbyState(int Capacity, int Count, IReadOnlyList<MatchPlayer> Players);

public class LobbyManager
{
    private const int Capacity = 2;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _occupants = [];

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

            var startMatch = false;

            if (_occupants.Count == Capacity)
            {
                _occupants.Clear();
                startMatch = true;
            }

            return (new LobbyState
            (
                Capacity: Capacity,
                Count: players.Count,
                Players: players
            ), startMatch);
        }
    }

    public LobbyState? Remove(string connectionId)
    {
        lock (_gate)
        {
            if (_occupants.Remove(connectionId))
            {
                return new(Capacity, _occupants.Count, GetPlayers());
            }
            else
            {
                return null;
            }
        }
    }

    private IReadOnlyList<MatchPlayer> GetPlayers()
    {
        return [.. _occupants.Select(occupant => new MatchPlayer(occupant.Key, occupant.Value))];
    }
}
