public class GameLogic
{
    private Player player1;
    private Player player2;

    public GameLogic()
    {
        player1 = new Player();
        player2 = new Player();
    }

    public void Start()
    {
        Deck deck = new Deck();
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

    public void PlayRound()
    {
        if (player1.CardCount() == 0)
        {
            Console.WriteLine("2-asis žaidėjas laimi žaidimą!");
            return;
        }

        if (player2.CardCount() == 0)
        {
            Console.WriteLine("1-asis žaidėjas laimi žaidimą!");
            return;
        }

        List<Card> warCards = new List<Card>();
        Card card1 = player1.DrawCard();
        Card card2 = player2.DrawCard();

        Console.WriteLine($"Player 1: {card1.Value}");
        Console.WriteLine($"Player 2: {card2.Value}");

        if (card1.Value > card2.Value)
        {
            Console.WriteLine("1-asis žaidėjas laimi!");
            player1.AddCard(card1);
            player1.AddCard(card2);
        }
        else if (card2.Value > card1.Value)
        {
            Console.WriteLine("2-asis žaidėjas laimi!");
            player2.AddCard(card1);
            player2.AddCard(card2);
        }
        else
        {
            while(true)
            {
                Console.WriteLine("KARAS!");
            
                if (player1.CardCount() < 2)
                {  
                    Console.WriteLine("2-asis žaidėjas laimi žaidimą!");
                    return;
                }

                if (player2.CardCount() < 2)
                {
                    Console.WriteLine("1-asis žaidėjas laimi žaidimą!");
                    return;
                }

                warCards.Add(card1);
                warCards.Add(card2);
                card1 = player1.DrawCard();
                card2 = player2.DrawCard();
                warCards.Add(card1);
                warCards.Add(card2);
                card1 = player1.DrawCard();
                card2 = player2.DrawCard();
                Console.WriteLine($"Player 1: {card1.Value}");
                Console.WriteLine($"Player 2: {card2.Value}");

                if (card1.Value > card2.Value)
                {
                    Console.WriteLine("1-asis žaidėjas laimi!");
                    for(int i = 0; i < warCards.Count; i++)
                    {
                        player1.AddCard(warCards[i]);
                    }
                    player1.AddCard(card1);
                    player1.AddCard(card2);
                    break;
                }
                else if (card2.Value > card1.Value)
                {
                    Console.WriteLine("2-asis žaidėjas laimi!");
                    for(int i = 0; i < warCards.Count; i++)
                    {
                        player2.AddCard(warCards[i]);
                    }
                    player2.AddCard(card1);
                    player2.AddCard(card2);
                    break;
                }
            }
        }
    }
}
