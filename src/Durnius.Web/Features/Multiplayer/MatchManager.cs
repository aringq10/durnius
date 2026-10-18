using Durnius.Web.Engines.Karas;

namespace Durnius.Web.Features.Multiplayer;

public sealed record MatchPlayer(string ConnectionId, string Username);

public sealed record MatchStart(MatchPlayer Player1, MatchPlayer Player2, int Player1Count, int Player2Count);

public sealed class DrawOutcome
{
    public required MatchPlayer Player1 { get; init; }
    public required MatchPlayer Player2 { get; init; }
    public required bool Player1Drawn { get; init; }
    public required bool Player2Drawn { get; init; }
    /// <summary>Set when both players drew and the round was resolved.</summary>
    public RoundResult? Round { get; init; }
    public required bool MatchEnded { get; init; }
}

public class MatchManager
{
    private readonly Lock _gate = new();

    private GameEngine? _game;
    private MatchPlayer? _player1;
    private MatchPlayer? _player2;
    private bool _running;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _running;
            }
        }
    }

    public MatchStart? TryStart(IReadOnlyList<MatchPlayer> members)
    {
        lock (_gate)
        {
            if (_running || members.Count != 2)
            {
                return null;
            }

            _game = new GameEngine();
            _player1 = members[0];
            _player2 = members[1];
            _running = true;

            var counts = _game.CardCounts();
            return new MatchStart(_player1, _player2, counts.Player1, counts.Player2);
        }
    }

    public DrawOutcome? Draw(string connectionId)
    {
        lock (_gate)
        {
            if (!_running || _game is null || _player1 is null || _player2 is null)
            {
                return null;
            }

            RoundResult? round;
            bool player1Drawn, player2Drawn;

            if (connectionId == _player1.ConnectionId)
            {
                var r = _game.DrawCardP1();
                if (r.Card is null)
                {
                    return null;
                }
                else if (r.Result is null)
                {
                    round = r.Result;
                    player1Drawn = true;
                    player2Drawn = false;
                }
                else
                {
                    round = r.Result;
                    player1Drawn = true;
                    player2Drawn = true;
                }
            }
            else if (connectionId == _player2.ConnectionId)
            {
                var r = _game.DrawCardP2();
                if (r.Card is null)
                {
                    return null;
                }
                else if (r.Result is null)
                {
                    round = r.Result;
                    player1Drawn = false;
                    player2Drawn = true;
                }
                else
                {
                    round = r.Result;
                    player1Drawn = true;
                    player2Drawn = true;
                }
            }
            else
            {
                return null;
            }

            if (round is not null && round.GameOver)
            {
                _running = false;
            }

            return new DrawOutcome
            {
                Player1 = _player1,
                Player2 = _player2,
                Player1Drawn = player1Drawn,
                Player2Drawn = player2Drawn,
                Round = round,
                MatchEnded = !_running
            };
        }
    }

    /// <summary>
    /// Aborts the running match because a player disconnected.
    /// Returns the remaining player when a running match was aborted, otherwise null.
    /// </summary>
    public (MatchPlayer Opponent, int Winner)? End(string connectionId)
    {
        lock (_gate)
        {
            if (!_running || _player1 is null || _player2 is null)
            {
                return null;
            }

            MatchPlayer? opponent = null;
            var winner = 0;
            if (connectionId == _player1.ConnectionId)
            {
                opponent = _player2;
                winner = 2;
            }
            else if (connectionId == _player2.ConnectionId)
            {
                opponent = _player1;
                winner = 1;
            }

            if (opponent is null)
            {
                return null;
            }

            _running = false;
            return (opponent, winner);
        }
    }
}
