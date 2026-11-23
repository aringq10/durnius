using Durnius.Web.Engines;
using Durnius.Web.Engines.Karas;
using Xunit;

namespace Durnius.Web.Tests.Engines.Karas;

public class KarasGameEngineTests
{
    private static Card C(Suit suit, Value value) => new(suit, value);

    [Fact]
    public void PublicConstructorSplitsTheDeckEvenly()
    {
        var game = new GameEngine(seed: 11);

        var counts = game.CardCounts();

        Assert.Equal(26, counts.Player1);
        Assert.Equal(26, counts.Player2);
    }

    [Fact]
    public void CardCountsReflectTheInjectedHands()
    {
        var game = new GameEngine(
            [C(Suit.Hearts, Value.Two), C(Suit.Diamonds, Value.Three)],
            [C(Suit.Clubs, Value.Four)]);

        var counts = game.CardCounts();

        Assert.Equal(2, counts.Player1);
        Assert.Equal(1, counts.Player2);
    }

    [Fact]
    public void RoundIsResolvedOnlyAfterBothPlayersDraw()
    {
        var game = new GameEngine(
            [C(Suit.Hearts, Value.Ten), C(Suit.Hearts, Value.Nine)],
            [C(Suit.Spades, Value.Three), C(Suit.Spades, Value.Four)]);

        var (card, result) = game.DrawCardP1();

        Assert.Equal(C(Suit.Hearts, Value.Ten), card);
        Assert.Null(result);
        Assert.Null(game.DrawCardP1().Card);

        var (otherCard, round) = game.DrawCardP2();

        Assert.Equal(C(Suit.Spades, Value.Three), otherCard);
        Assert.NotNull(round);
        Assert.Equal(1, round.RoundWinner);
        Assert.Equal(C(Suit.Hearts, Value.Ten), round.Player1Card);
        Assert.Equal(C(Suit.Spades, Value.Three), round.Player2Card);
        Assert.Empty(round.War);
        Assert.Equal(3, round.Player1Count);
        Assert.Equal(1, round.Player2Count);
        Assert.False(round.GameOver);
        Assert.Equal(0, round.GameWinner);

        // The round cleared both pending draws, so a player can draw again.
        Assert.Equal(C(Suit.Spades, Value.Four), game.DrawCardP2().Card);
    }

    [Fact]
    public void PlayerTwoCanResolveARoundStartedByPlayerOne()
    {
        var game = new GameEngine(
            [C(Suit.Hearts, Value.Two)],
            [C(Suit.Spades, Value.Ace)]);

        Assert.Equal(C(Suit.Spades, Value.Ace), game.DrawCardP2().Card);
        var (_, round) = game.DrawCardP1();

        Assert.NotNull(round);
        Assert.Equal(2, round.RoundWinner);
        Assert.Equal(0, round.Player1Count);
        Assert.Equal(2, round.Player2Count);
    }

    [Fact]
    public void GameEndsWhenAPlayerRunsOutOfCards()
    {
        var game = new GameEngine(
            [C(Suit.Hearts, Value.Two)],
            [C(Suit.Spades, Value.Three)]);

        game.DrawCardP1();
        var (_, round) = game.DrawCardP2();

        Assert.NotNull(round);
        Assert.Equal(2, round.RoundWinner);
        Assert.True(round.GameOver);
        // Player 1 emptied their hand, so player 2 wins the game.
        Assert.Equal(2, round.GameWinner);
        Assert.Equal(0, round.Player1Count);
        Assert.Equal(2, round.Player2Count);
        Assert.Null(game.DrawCardP1().Card);
        Assert.Null(game.DrawCardP2().Card);
    }

    [Fact]
    public void EqualCardsStartAWarAndTheWarWinnerTakesEveryCard()
    {
        var game = new GameEngine(
            [C(Suit.Hearts, Value.Seven), C(Suit.Hearts, Value.Two), C(Suit.Hearts, Value.Three)],
            [C(Suit.Spades, Value.Seven), C(Suit.Clubs, Value.Two), C(Suit.Clubs, Value.Four)]);

        game.DrawCardP1();
        var (_, round) = game.DrawCardP2();

        Assert.NotNull(round);
        Assert.Equal(2, round.RoundWinner);
        Assert.Equal(2, round.War.Count);
        Assert.Equal(Value.Two, round.War[0].Player1Card.Value);
        Assert.Equal(Value.Two, round.War[0].Player2Card.Value);
        Assert.Equal(Value.Three, round.War[1].Player1Card.Value);
        Assert.Equal(Value.Four, round.War[1].Player2Card.Value);
        Assert.Equal(0, round.Player1Count);
        Assert.Equal(6, round.Player2Count);
        Assert.True(round.GameOver);
        Assert.Equal(2, round.GameWinner);
    }

    [Fact]
    public void WarRepeatsUntilTheCardsAreDecidedOrAPlayerRunsOut()
    {
        var game = new GameEngine(
            [C(Suit.Hearts, Value.Seven), C(Suit.Hearts, Value.Two), C(Suit.Hearts, Value.Three), C(Suit.Hearts, Value.Four)],
            [C(Suit.Spades, Value.Seven), C(Suit.Clubs, Value.Two), C(Suit.Clubs, Value.Three), C(Suit.Clubs, Value.Five)]);

        game.DrawCardP1();
        var (_, round) = game.DrawCardP2();

        Assert.NotNull(round);
        Assert.Equal(2, round.War.Count);
        Assert.Equal(Value.Two, round.War[0].Player1Card.Value);
        Assert.Equal(Value.Three, round.War[1].Player1Card.Value);
        Assert.Equal(Value.Three, round.War[1].Player2Card.Value);
        // Both players had only one card left for another war, so player 1
        // (the player with fewer cards) loses the pot and the game.
        Assert.Equal(2, round.RoundWinner);
        Assert.Equal(1, round.Player1Count);
        Assert.Equal(7, round.Player2Count);
        Assert.True(round.GameOver);
        Assert.Equal(2, round.GameWinner);
    }

    [Fact]
    public void WarEndsImmediatelyWhenAPlayerCannotGoOn()
    {
        var game = new GameEngine(
            [C(Suit.Hearts, Value.Seven), C(Suit.Hearts, Value.Two), C(Suit.Hearts, Value.Three), C(Suit.Hearts, Value.Four)],
            [C(Suit.Spades, Value.Seven), C(Suit.Clubs, Value.Two)]);

        game.DrawCardP1();
        var (_, round) = game.DrawCardP2();

        Assert.NotNull(round);
        Assert.Empty(round.War);
        Assert.Equal(1, round.RoundWinner);
        Assert.Equal(5, round.Player1Count);
        // Player 2 keeps their unused face-down card when the war is cut short.
        Assert.Equal(1, round.Player2Count);
        Assert.True(round.GameOver);
        Assert.Equal(1, round.GameWinner);
    }
}
