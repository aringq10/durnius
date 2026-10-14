namespace Durnius.Web.Engines.Karas;

public sealed record WarExchange(Card Player1Card, Card Player2Card);

public sealed class RoundResult
{
    /// <summary>1 or 2 when a round was played.</summary>
    public int RoundWinner { get; init; }
    public required Card Player1Card { get; init; }
    public required Card Player2Card { get; init; }
    /// <summary>Extra pairs compared after the initial draw tied (empty when no war happened).</summary>
    public IReadOnlyList<WarExchange> War { get; init; } = Array.Empty<WarExchange>();
    public int Player1Count { get; init; }
    public int Player2Count { get; init; }
    public bool GameOver { get; init; }
    /// <summary>1 or 2 when <see cref="GameOver"/> is true, otherwise 0.</summary>
    public int GameWinner { get; init; }
}

public class GameEngine
{
    private readonly Player player1 = new();
    private readonly Player player2 = new();

    private Card? player1Drawn;
    private Card? player2Drawn;

    private bool gameOver;
    private int gameWinner;

    public GameEngine()
    {
        Deck deck = new();
        deck.Shuffle();

        while (deck.Count() > 0)
        {
            Card card = deck.DrawCard();
            if(card.Suit == Suit.hearts || card.Suit == Suit.diamonds)
            {
                player1.AddCard(card);
            }
            else
            {
                player2.AddCard(card);
            }
        }
    }

    public (int Player1, int Player2) CardCounts()
    {
        return (player1.CardCount(), player2.CardCount());
    }

    public (Card? Card, RoundResult? Result) DrawCardP1()
    {
        if (player1Drawn is not null || gameOver)
        {
            return (null, null);
        }

        var drawn = player1.DrawCard();
        player1Drawn = drawn;
        RoundResult? r = null;

        if (player1Drawn is not null && player2Drawn is not null)
        {
            r = PlayRound();
            player1Drawn = null;
            player2Drawn = null;
        }

        return (drawn, r);
    }

    public (Card? Card, RoundResult? Result) DrawCardP2()
    {
        if (player2Drawn is not null || gameOver)
        {
            return (null, null);
        }

        var drawn = player2.DrawCard();
        player2Drawn = drawn;
        RoundResult? r = null;

        if (player1Drawn is not null && player2Drawn is not null)
        {
            r = PlayRound();
            player1Drawn = null;
            player2Drawn = null;
        }

        return (drawn, r);
    }

    private RoundResult PlayRound()
    {
        Card card1 = player1Drawn!;
        Card card2 = player2Drawn!;
        Card initialCard1 = card1;
        Card initialCard2 = card2;

        var war = new List<WarExchange>();
        var warCards = new List<Card>();
        int roundWinner;
        gameWinner = 0;

        if (card1.Value > card2.Value)
        {
            player1.AddCard(card1);
            player1.AddCard(card2);
            roundWinner = 1;
        }
        else if (card2.Value > card1.Value)
        {
            player2.AddCard(card1);
            player2.AddCard(card2);
            roundWinner = 2;
        }
        else
        {
            roundWinner = 0;

            while (true)
            {
                if (player1.CardCount() < 2)
                {
                    gameWinner = 2;
                    break;
                }

                if (player2.CardCount() < 2)
                {
                    gameWinner = 1;
                    break;
                }

                warCards.Add(card1);
                warCards.Add(card2);
                card1 = player1.DrawCard();
                card2 = player2.DrawCard();
                warCards.Add(card1);
                warCards.Add(card2);
                war.Add(new WarExchange(card1, card2));
                card1 = player1.DrawCard();
                card2 = player2.DrawCard();
                war.Add(new WarExchange(card1, card2));

                if (card1.Value > card2.Value)
                {
                    for (int i = 0; i < warCards.Count; i++)
                    {
                        player1.AddCard(warCards[i]);
                    }
                    player1.AddCard(card1);
                    player1.AddCard(card2);
                    roundWinner = 1;
                    break;
                }

                if (card2.Value > card1.Value)
                {
                    for (int i = 0; i < warCards.Count; i++)
                    {
                        player2.AddCard(warCards[i]);
                    }
                    player2.AddCard(card1);
                    player2.AddCard(card2);
                    roundWinner = 2;
                    break;
                }
            }

            if (gameWinner != 0)
            {
                roundWinner = gameWinner;
            }
        }

        int count1 = player1.CardCount();
        int count2 = player2.CardCount();

        if (gameWinner == 0 && count1 == 0) gameWinner = 2;
        if (gameWinner == 0 && count2 == 0) gameWinner = 1;

        gameOver = gameWinner != 0;

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
            Player1Count = player1.CardCount(),
            Player2Count = player2.CardCount(),
            GameOver = gameOver,
            GameWinner = gameWinner,
        };
    }
}
