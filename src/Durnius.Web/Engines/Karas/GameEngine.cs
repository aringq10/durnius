namespace Durnius.Web.Engines.Karas;

public sealed record WarExchange(Card Player1Card, Card Player2Card);

public sealed class RoundResult
{
    /// <summary>1 or 2 when a round was played.</summary>
    public int RoundWinner { get; init; }
    public required Card Player1Card { get; init; }
    public required Card Player2Card { get; init; }
    /// <summary>Extra pairs compared after the initial draw tied (empty when no war happened).</summary>
    public IReadOnlyList<WarExchange> War { get; init; } = [];
    public int Player1Count { get; init; }
    public int Player2Count { get; init; }
    public bool GameOver { get; init; }
    /// <summary>1 or 2 when <see cref="GameOver"/> is true, otherwise 0.</summary>
    public int GameWinner { get; init; }
}

public class GameEngine
{
    private readonly Player _player1 = new();
    private readonly Player _player2 = new();

    private Card? _player1Drawn;
    private Card? _player2Drawn;

    private bool _gameOver;
    private int _gameWinner;

    public GameEngine(int? seed = null)
    {
        Deck deck = new();
        deck.Shuffle(seed: seed);

        while (deck.Count() > 0)
        {
            var card = deck.DrawCard();
            if (card.Suit.IsRed())
            {
                _player1.AddCard(card);
            }
            else
            {
                _player2.AddCard(card);
            }
        }
    }

    public CardCount CardCounts()
    {
        return new CardCount(_player1.CardCount(), _player2.CardCount());
    }

    public (Card? Card, RoundResult? Result) DrawCardP1()
    {
        if (_player1Drawn is not null || _gameOver)
        {
            return (null, null);
        }

        var drawn = _player1.DrawCard();
        _player1Drawn = drawn;
        RoundResult? r = null;

        if (_player1Drawn is not null && _player2Drawn is not null)
        {
            r = PlayRound();
            _player1Drawn = null;
            _player2Drawn = null;
        }

        return (drawn, r);
    }

    public (Card? Card, RoundResult? Result) DrawCardP2()
    {
        if (_player2Drawn is not null || _gameOver)
        {
            return (null, null);
        }

        var drawn = _player2.DrawCard();
        _player2Drawn = drawn;
        RoundResult? r = null;

        if (_player1Drawn is not null && _player2Drawn is not null)
        {
            r = PlayRound();
            _player1Drawn = null;
            _player2Drawn = null;
        }

        return (drawn, r);
    }

    private RoundResult PlayRound()
    {
        var card1 = _player1Drawn!;
        var card2 = _player2Drawn!;
        var initialCard1 = card1;
        var initialCard2 = card2;

        var war = new List<WarExchange>();
        var warCards = new List<Card>();
        int roundWinner;
        _gameWinner = 0;

        if (card1.Value > card2.Value)
        {
            _player1.AddCard(card1);
            _player1.AddCard(card2);
            roundWinner = 1;
        }
        else if (card2.Value > card1.Value)
        {
            _player2.AddCard(card1);
            _player2.AddCard(card2);
            roundWinner = 2;
        }
        else
        {
            roundWinner = 0;

            while (true)
            {
                if (_player1.CardCount() < 2 || _player2.CardCount() < 2)
                {
                    _gameWinner = _player1.CardCount() < 2 ? 2 : 1;

                    warCards.Add(card1);
                    warCards.Add(card2);

                    var potWinner = _gameWinner == 1 ? _player1 : _player2;
                    foreach (var card in warCards)
                    {
                        potWinner.AddCard(card);
                    }

                    break;
                }

                warCards.Add(card1);
                warCards.Add(card2);
                card1 = _player1.DrawCard();
                card2 = _player2.DrawCard();
                warCards.Add(card1);
                warCards.Add(card2);
                war.Add(new WarExchange(card1, card2));
                card1 = _player1.DrawCard();
                card2 = _player2.DrawCard();
                war.Add(new WarExchange(card1, card2));

                if (card1.Value > card2.Value)
                {
                    for (var i = 0; i < warCards.Count; i++)
                    {
                        _player1.AddCard(warCards[i]);
                    }
                    _player1.AddCard(card1);
                    _player1.AddCard(card2);
                    roundWinner = 1;
                    break;
                }

                if (card2.Value > card1.Value)
                {
                    for (var i = 0; i < warCards.Count; i++)
                    {
                        _player2.AddCard(warCards[i]);
                    }
                    _player2.AddCard(card1);
                    _player2.AddCard(card2);
                    roundWinner = 2;
                    break;
                }
            }

            if (_gameWinner != 0)
            {
                roundWinner = _gameWinner;
            }
        }

        var count1 = _player1.CardCount();
        var count2 = _player2.CardCount();

        if (_gameWinner == 0 && count1 == 0)
        {
            _gameWinner = 2;
        }

        if (_gameWinner == 0 && count2 == 0)
        {
            _gameWinner = 1;
        }

        _gameOver = _gameWinner != 0;

        return Result(roundWinner, initialCard1, initialCard2, war);
    }

    private RoundResult Result(int roundWinner, Card player1Card, Card player2Card, IReadOnlyList<WarExchange> war)
    {
        return new RoundResult
        {
            RoundWinner = roundWinner,
            Player1Card = player1Card,
            Player2Card = player2Card,
            War = war,
            Player1Count = _player1.CardCount(),
            Player2Count = _player2.CardCount(),
            GameOver = _gameOver,
            GameWinner = _gameWinner,
        };
    }
}
