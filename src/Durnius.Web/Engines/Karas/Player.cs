namespace Durnius.Web.Engines.Karas;

public class Player
{
    private readonly List<Card> _cards = [];

    public Card DrawCard()
    {
        var card = _cards[0];
        _cards.RemoveAt(0);

        return card;
    }

    public void AddCard(Card card)
    {
        _cards.Add(card);
    }

    public int CardCount()
    {
        return _cards.Count;
    }
}
