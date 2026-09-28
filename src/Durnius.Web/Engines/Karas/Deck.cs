public class Deck
{
    private List<Card> cards = new();

    public Deck()
    {
        CreateDeck();
    }

    private void CreateDeck()
    {
        foreach (Suit suit in Enum.GetValues<Suit>())
        {
            foreach (Value value in Enum.GetValues<Value>())
            {
                cards.Add(new Card(suit, value));
            }
        }
    }

    public void Shuffle()
    {
        Random random = new();

        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);

            Card temp = cards[i];
            cards[i] = cards[j];
            cards[j] = temp;
        }
    }

    public Card DrawCard()
    {
        Card card = cards[0];
        cards.RemoveAt(0);

        return card;
    }

    public int Count()
    {
        return cards.Count;
    }
}