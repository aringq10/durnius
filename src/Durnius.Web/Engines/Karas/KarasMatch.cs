using Durnius.Web.Features.Multiplayer;

namespace Durnius.Web.Engines.Karas;

public sealed record MatchStart(int You, string Player1, string Player2, int Player1Count, int Player2Count);

public record DrawStatus(bool player1Drawn, bool player2Drawn);

public record RoundResultDto(
    int RoundWinner,
    CardDto Player1Card,
    CardDto Player2Card,
    WarDto[] War,
    int Player1Count,
    int Player2Count,
    bool GameOver,
    int GameWinner);

public record WarDto(CardDto Player1, CardDto Player2);

public record CardDto
{
    public string Suit { get; init; }
    public string Value { get; init; }

    public CardDto(Card card)
    {
        Suit = card.Suit.ToString().ToLowerInvariant();
        Value = card.Value.ToString().ToLowerInvariant();
    }
}

public class KarasMatch : IMatch<KarasMatch>
{
    private readonly Lock _gate = new();

    private readonly GameEngine _game = new();
    private readonly MatchPlayer _player1;
    private readonly MatchPlayer _player2;
    private bool _running = true;

    private KarasMatch(MatchPlayer player1, MatchPlayer player2)
    {
        _player1 = player1;
        _player2 = player2;
    }

    public static (KarasMatch, Response) Create(MatchOptions options)
    {
        if (options.Players.Count != 2)
        {
            throw new ArgumentException("Karas needs exactly 2 players.");
        }

        var match = new KarasMatch(options.Players[0], options.Players[1]);
        var counts = match._game.CardCounts();

        return (match, MatchStartedResponse(match._player1, match._player2, counts.Player1, counts.Player2));
    }

    public Response MakeMove(string connectionId, object move)
    {
        lock (_gate)
        {
            if (!_running)
            {
                return new();
            }

            RoundResult? round;
            bool player1Drawn, player2Drawn;

            if (connectionId == _player1.ConnectionId)
            {
                var r = _game.DrawCardP1();
                if (r.Card is null)
                {
                    return new();
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
                    return new();
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
                return new();
            }

            if (round is not null && round.GameOver)
            {
                _running = false;
            }

            List<Message> messages = [
                DrawStatusMessage(player1Drawn, player2Drawn)
            ];

            if (round is not null)
            {
                messages.Add(RoundResultMessage(round));
            }

            return new(messages);
        }
    }

    public Response Disconnect(string connectionId)
    {
        lock (_gate)
        {
            if (!_running)
            {
                return new();
            }

            int winner;
            if (connectionId == _player1.ConnectionId)
            {
                winner = 2;
            }
            else if (connectionId == _player2.ConnectionId)
            {
                winner = 1;
            }
            else
            {
                return new();
            }

            _running = false;
            return MatchEndedResponse(winner);
        }
    }

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

    private static RoundResultDto ToDto(RoundResult round) => new(
        round.RoundWinner,
        new(round.Player1Card),
        new(round.Player2Card),
        round.War.Select(w => new WarDto(new(w.Player1Card), new(w.Player2Card))).ToArray(),
        round.Player1Count,
        round.Player2Count,
        round.GameOver,
        round.GameWinner);

    private static Response MatchStartedResponse(MatchPlayer player1, MatchPlayer player2, int player1Count, int player2Count)
    {
        var player1Message = new Message(
            "MatchStarted",
            new MatchStart(1, player1.Username, player2.Username, player1Count, player2Count)
        );

        var player2Message = new Message(
            "MatchStarted",
            new MatchStart(2, player1.Username, player2.Username, player1Count, player2Count)
        );

        return new(
            privateByPlayer: new Dictionary<MatchPlayer, Message> {
                [player1] = player1Message,
                [player2] = player2Message
            }
        );
    }

    private static Response MatchEndedResponse(int winner)
    {
        return new([new("MatchEnded", winner)]);
    }

    private static Message DrawStatusMessage(bool player1Drawn, bool player2Drawn)
    {
        return new Message("DrawStatus", new DrawStatus(player1Drawn, player2Drawn));
    }

    private static Message RoundResultMessage(RoundResult round)
    {
        return new Message("RoundResult", ToDto(round));
    }
}
