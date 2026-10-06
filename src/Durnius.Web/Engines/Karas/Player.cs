namespace Durnius.Web.Engines.Karas;

public class Player
{
    private List<Card> cards = new();

    public Card DrawCard()
    {
        Card card = cards[0];
        cards.RemoveAt(0);

        return card;
    }

    public void AddCard(Card card)
    {
        cards.Add(card);
    }

    public int CardCount()
    {
        return cards.Count;
    }
}