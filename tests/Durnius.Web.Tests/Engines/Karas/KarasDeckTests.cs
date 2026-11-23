using Durnius.Web.Engines;
using Durnius.Web.Engines.Karas;
using Xunit;

namespace Durnius.Web.Tests.Engines.Karas;

public class KarasDeckTests
{
    [Fact]
    public void DeckContainsEveryCardExactlyOnce()
    {
        var deck = new Deck();

        var cards = deck.ToArray();

        Assert.Equal(52, deck.Count());
        Assert.Equal(52, cards.Distinct().Count());
        Assert.All(Enum.GetValues<Suit>(), suit =>
            Assert.Equal(13, cards.Count(card => card.Suit == suit)));
        Assert.All(Enum.GetValues<Value>(), value =>
            Assert.Equal(4, cards.Count(card => card.Value == value)));
    }

    [Fact]
    public void ShuffleIsDeterministicForTheSameSeed()
    {
        var first = new Deck();
        var second = new Deck();

        first.Shuffle(42);
        second.Shuffle(42);

        Assert.Equal(first, second);
        Assert.NotEqual(first, new Deck());
    }

    [Fact]
    public void DrawCardRemovesFromTheFrontAndThrowsWhenDeckIsEmpty()
    {
        var deck = new Deck();
        deck.Shuffle(7);
        var expected = deck.First();

        var drawn = deck.DrawCard();

        Assert.Equal(expected, drawn);
        Assert.Equal(51, deck.Count());
        for (var i = 0; i < 51; i++)
        {
            deck.DrawCard();
        }

        Assert.Equal(0, deck.Count());
        Assert.Throws<ArgumentOutOfRangeException>(() => deck.DrawCard());
    }
}
