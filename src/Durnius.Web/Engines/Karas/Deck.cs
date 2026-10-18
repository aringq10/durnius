namespace Durnius.Web.Engines.Karas;

public class Deck
{
    private readonly List<Card> _cards = [];

    public Deck()
    {
        foreach (var suit in Enum.GetValues<Suit>())
        {
            foreach (var value in Enum.GetValues<Value>())
            {
                _cards.Add(new Card(suit, value));
            }
        }
    }

    public void Shuffle(int? seed = null)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();

        for (var i = _cards.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);

            (_cards[j], _cards[i]) = (_cards[i], _cards[j]);
        }
    }

    public Card DrawCard()
    {
        var card = _cards[0];
        _cards.RemoveAt(0);

        return card;
    }

    public int Count()
    {
        return _cards.Count;
    }
}
