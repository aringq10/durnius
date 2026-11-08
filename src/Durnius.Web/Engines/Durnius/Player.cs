namespace Durnius.Web.Engines.Durnius;

public class Player
{
    private readonly List<Card> _cards = [];

    public IReadOnlyList<Card> Cards => _cards.ToArray();

    public int CardCount() => _cards.Count;

    internal bool HasCard(Card card) => _cards.Contains(card);

    internal void RemoveCard(Card card)
    {
        if (!_cards.Remove(card))
        {
            throw new InvalidOperationException("The player does not have that card.");
        }
    }

    internal void AddCard(Card card) => _cards.Add(card);
}
