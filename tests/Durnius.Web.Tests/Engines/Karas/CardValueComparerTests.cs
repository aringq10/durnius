using Durnius.Web.Engines;
using Durnius.Web.Engines.Karas;
using Xunit;

namespace Durnius.Web.Tests.Engines.Karas;

public class CardValueComparerTests
{
    private readonly CardValueComparer _comparer = new();

    [Fact]
    public void ComparesByValueOnly()
    {
        Assert.True(_comparer.Compare(new Card(Suit.Hearts, Value.Two), new Card(Suit.Spades, Value.Two)) == 0);
        Assert.True(_comparer.Compare(new Card(Suit.Clubs, Value.Ace), new Card(Suit.Diamonds, Value.King)) > 0);
        Assert.True(_comparer.Compare(new Card(Suit.Clubs, Value.Three), new Card(Suit.Diamonds, Value.Four)) < 0);
    }

    [Fact]
    public void HandlesNulls()
    {
        var card = new Card(Suit.Hearts, Value.Ace);

        Assert.Equal(0, _comparer.Compare(null, null));
        Assert.True(_comparer.Compare(null, card) < 0);
        Assert.True(_comparer.Compare(card, null) > 0);
    }
}
