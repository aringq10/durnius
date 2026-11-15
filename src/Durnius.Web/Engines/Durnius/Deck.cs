using System.Collections;

namespace Durnius.Web.Engines.Durnius;

public class Deck : IEnumerable<Card>
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

    public void Shuffle(int? seed = null) =>
        Shuffle(seed.HasValue ? new Random(seed.Value) : Random.Shared);

    internal void Shuffle(Random random)
    {
        for (var i = _cards.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (_cards[j], _cards[i]) = (_cards[i], _cards[j]);
        }
    }

    public Card DrawCard()
    {
        if (_cards.Count == 0)
        {
            throw new InvalidOperationException("The deck is empty.");
        }

        var card = _cards[0];
        _cards.RemoveAt(0);
        return card;
    }

    public Card? BottomCard => _cards.Count == 0 ? null : _cards[^1];

    public int Count() => _cards.Count;

    public IEnumerator<Card> GetEnumerator() => _cards.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
