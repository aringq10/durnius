using Durnius.Web.Engines;
using Durnius.Web.Engines.Durnius;
using Xunit;

namespace Durnius.Web.Tests.Engines.Durnius;

public sealed class DurniusDeckTests
{
    [Fact]
    public void DeckContainsEveryCardOnce()
    {
        var cards = new Deck().ToArray();

        Assert.Equal(52, cards.Length);
        Assert.Equal(52, cards.Distinct().Count());
    }

    [Fact]
    public void ShuffleIsDeterministicAndBottomCardIsDrawnLast()
    {
        var first = new Deck();
        first.Shuffle(1234);
        var second = new Deck();
        second.Shuffle(1234);

        Assert.Equal(first.ToArray(), second.ToArray());

        var bottom = first.BottomCard;
        for (var i = 0; i < 51; i++)
        {
            first.DrawCard();
        }

        Assert.Equal(bottom, first.DrawCard());
        Assert.Throws<InvalidOperationException>(() => first.DrawCard());
    }
}

public sealed class DurniusGameEngineTests
{
    private static Card C(Suit suit, Value value) => new(suit, value);

    private static GameEngine NewGame(
        Card[][] hands,
        Suit trump = Suit.Hearts,
        int attacker = 0,
        Card[]? talon = null,
        int handSize = 1)
    {
        talon ??= [C(trump, Value.Two)];
        return new GameEngine(hands, talon, trump, attacker, handSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void PlayerCountMustBeBetweenTwoAndSix(int playerCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameEngine(playerCount));
    }

    [Fact]
    public void ConstructorDealsSixCardsEachAndKeepsTrumpCardInTalon()
    {
        var game = new GameEngine(4, seed: 7);

        Assert.Equal(new[] { 6, 6, 6, 6 }, game.CardCounts());
        Assert.Equal(28, game.DeckCount);
        Assert.Equal(game.TrumpCard.Suit, game.TrumpSuit);

        var allCards = Enumerable.Range(0, game.PlayerCount)
            .SelectMany(game.PlayerCards)
            .ToArray();
        var trumps = allCards.Where(card => card.Suit == game.TrumpSuit).ToArray();
        var openingCard = trumps.Length > 0 ? trumps.Min()! : allCards.Min()!;

        Assert.Contains(openingCard, game.PlayerCards(game.CurrentAttacker));

        Assert.Equal((game.CurrentAttacker + 1) % game.PlayerCount, game.CurrentDefender);
    }

    [Fact]
    public void OpeningAttackCanOnlyBePlayedByTheAttacker()
    {
        var game = NewGame([
            [C(Suit.Clubs, Value.Six)],
            [C(Suit.Diamonds, Value.Six)],
        ]);

        Assert.Throws<InvalidOperationException>(() =>
            game.PlayAttack(1, C(Suit.Diamonds, Value.Six)));
        Assert.Null(game.PlayAttack(0, C(Suit.Clubs, Value.Six)));
        Assert.Equal(
            new[] { new PlayedCard(0, C(Suit.Clubs, Value.Six), null) },
            game.CardsOnTable());
    }

    [Fact]
    public void AddedAttackMustMatchRankAlreadyOnTable()
    {
        var clubsSix = C(Suit.Clubs, Value.Six);
        var diamondsSix = C(Suit.Diamonds, Value.Six);
        var heartsSeven = C(Suit.Hearts, Value.Seven);
        var spadesSix = C(Suit.Spades, Value.Six);

        var game = NewGame([
            [clubsSix, diamondsSix, heartsSeven],
            [spadesSix, C(Suit.Spades, Value.Seven)],
        ]);
        game.PlayAttack(0, clubsSix);

        Assert.Throws<InvalidOperationException>(() => game.PlayAttack(0, heartsSeven));
        Assert.Throws<InvalidOperationException>(() => game.PlayAttack(1, spadesSix));
        Assert.Null(game.PlayAttack(0, diamondsSix));
        Assert.Equal(2, game.CardsOnTable().Count);
    }

    [Fact]
    public void DefenseCardsAlsoOpenTheirRankForAddedAttacks()
    {
        var clubsSix = C(Suit.Clubs, Value.Six);
        var clubsNine = C(Suit.Clubs, Value.Nine);

        var game = NewGame([
            [clubsSix, C(Suit.Diamonds, Value.Nine)],
            [clubsNine, C(Suit.Spades, Value.Seven), C(Suit.Hearts, Value.Eight)],
        ]);
        game.PlayAttack(0, clubsSix);
        Assert.Null(game.Defend(1, clubsSix, clubsNine));

        Assert.Null(game.PlayAttack(0, C(Suit.Diamonds, Value.Nine)));
        Assert.Equal(2, game.CardsOnTable().Count);
    }

    [Fact]
    public void DefenseRequiresDefenderAndMustBeatTheAttackCard()
    {
        var clubsSix = C(Suit.Clubs, Value.Six);
        var clubsSeven = C(Suit.Clubs, Value.Seven);
        var heartsSeven = C(Suit.Hearts, Value.Seven);

        var game = NewGame([
            [clubsSix],
            [clubsSeven, heartsSeven, C(Suit.Diamonds, Value.Ace)],
        ]);
        game.PlayAttack(0, clubsSix);

        Assert.Throws<InvalidOperationException>(() => game.Defend(0, clubsSix, clubsSeven));
        Assert.Throws<InvalidOperationException>(() =>
            game.Defend(1, clubsSix, C(Suit.Diamonds, Value.Ace)));
        Assert.NotNull(game.Defend(1, clubsSix, heartsSeven));
        Assert.Throws<InvalidOperationException>(() => game.Defend(1, clubsSix, clubsSeven));
    }

    [Fact]
    public void TrumpAttackCanOnlyBeBeatenByHigherTrump()
    {
        var heartsSix = C(Suit.Hearts, Value.Six);
        var heartsFive = C(Suit.Hearts, Value.Five);
        var heartsSeven = C(Suit.Hearts, Value.Seven);

        var game = NewGame([
            [heartsSix],
            [heartsFive, heartsSeven, C(Suit.Diamonds, Value.Ace)],
        ]);
        game.PlayAttack(0, heartsSix);

        Assert.Throws<InvalidOperationException>(() =>
            game.Defend(1, heartsSix, C(Suit.Diamonds, Value.Ace)));
        Assert.Throws<InvalidOperationException>(() => game.Defend(1, heartsSix, heartsFive));
        Assert.NotNull(game.Defend(1, heartsSix, heartsSeven));
    }

    [Fact]
    public void AttackCannotExceedDefenderHandSize()
    {
        var clubsSix = C(Suit.Clubs, Value.Six);
        var clubsSeven = C(Suit.Clubs, Value.Seven);
        var diamondsSix = C(Suit.Diamonds, Value.Six);
        var spadesSix = C(Suit.Spades, Value.Six);

        var game = NewGame([
            [clubsSix, diamondsSix, spadesSix, C(Suit.Diamonds, Value.Seven)],
            [clubsSeven, C(Suit.Hearts, Value.Seven), C(Suit.Spades, Value.Seven)],
        ]);
        game.PlayAttack(0, clubsSix);
        Assert.Null(game.Defend(1, clubsSix, clubsSeven));
        game.PlayAttack(0, diamondsSix);
        game.PlayAttack(0, spadesSix);

        Assert.Throws<InvalidOperationException>(() =>
            game.PlayAttack(0, C(Suit.Diamonds, Value.Seven)));
    }

    [Fact]
    public void TakingCardsEndsRoundAndSnapshotsPlayedCards()
    {
        var clubsSix = C(Suit.Clubs, Value.Six);

        var game = NewGame(
            [[clubsSix], [C(Suit.Diamonds, Value.Six)]],
            talon: [C(Suit.Hearts, Value.Seven), C(Suit.Hearts, Value.Eight)]);
        game.PlayAttack(0, clubsSix);

        var result = game.TakeCards(1);

        Assert.True(result.DefenderTookCards);
        Assert.Equal(0, result.Attacker);
        Assert.Equal(1, result.Defender);
        Assert.Equal(new[] { new PlayedCard(0, clubsSix, null) }, result.CardsOnTable);
        Assert.Empty(game.CardsOnTable());
        Assert.Equal(new[] { 1, 2 }, result.PlayerCardCounts);
        Assert.Equal(new[] { 1, 2 }, game.CardCounts());
        Assert.Equal(0, game.CurrentAttacker);
        Assert.Equal(1, game.CurrentDefender);
    }

    [Fact]
    public void SuccessfulDefenseMakesDefenderTheNextAttacker()
    {
        var clubsSix = C(Suit.Clubs, Value.Six);

        var game = NewGame(
            [[clubsSix], [C(Suit.Clubs, Value.Seven)]],
            talon: [C(Suit.Hearts, Value.Three), C(Suit.Hearts, Value.Four)]);
        game.PlayAttack(0, clubsSix);

        var result = game.Defend(1, clubsSix, C(Suit.Clubs, Value.Seven));

        Assert.NotNull(result);
        Assert.False(result.DefenderTookCards);
        Assert.Equal(1, result.NextAttacker);
        Assert.Equal(1, game.CurrentAttacker);
        Assert.Equal(0, game.CurrentDefender);
        Assert.Equal(new[] { 1, 1 }, result.PlayerCardCounts);
    }

    [Fact]
    public void RoundEndsWhenAllAttackersHavePassedAfterDefense()
    {
        var clubsSix = C(Suit.Clubs, Value.Six);
        var clubsSeven = C(Suit.Clubs, Value.Seven);

        var game = NewGame(
            [[clubsSix, C(Suit.Diamonds, Value.Six)], [clubsSeven, C(Suit.Diamonds, Value.Seven)]],
            talon: [C(Suit.Hearts, Value.Three), C(Suit.Hearts, Value.Four)]);
        game.PlayAttack(0, clubsSix);
        Assert.Null(game.Defend(1, clubsSix, clubsSeven));
        Assert.Throws<InvalidOperationException>(() => game.PassAttack(1));

        var result = game.PassAttack(0);

        Assert.NotNull(result);
        Assert.False(result.DefenderTookCards);
        Assert.Equal(1, result.NextAttacker);
        Assert.Empty(game.CardsOnTable());
    }

    [Fact]
    public void PassingDoesNotEndAnUndefendedRound()
    {
        var clubsSix = C(Suit.Clubs, Value.Six);

        var game = NewGame([
            [clubsSix, C(Suit.Diamonds, Value.Six)],
            [C(Suit.Clubs, Value.Seven), C(Suit.Diamonds, Value.Seven)],
            [C(Suit.Spades, Value.Seven), C(Suit.Hearts, Value.Seven)],
        ]);
        game.PlayAttack(0, clubsSix);

        Assert.Null(game.PassAttack(0));
        Assert.Throws<InvalidOperationException>(() => game.PassAttack(0));
        Assert.Null(game.Defend(1, clubsSix, C(Suit.Clubs, Value.Seven)));
    }

    [Fact]
    public void TransferMakesDefenderTheAttackerAndMovesAttackToNextPlayer()
    {
        var clubsSix = C(Suit.Clubs, Value.Six);
        var diamondsSix = C(Suit.Diamonds, Value.Six);

        var game = NewGame([
            [clubsSix, C(Suit.Clubs, Value.Seven)],
            [diamondsSix, C(Suit.Diamonds, Value.Seven)],
            [C(Suit.Clubs, Value.Eight), C(Suit.Diamonds, Value.Eight)],
        ]);
        game.PlayAttack(0, clubsSix);

        Assert.Null(game.Transfer(1, diamondsSix));

        Assert.Equal(1, game.CurrentAttacker);
        Assert.Equal(2, game.CurrentDefender);
        Assert.Equal(
            new[]
            {
                new PlayedCard(0, clubsSix, null),
                new PlayedCard(1, diamondsSix, null),
            },
            game.CardsOnTable());
    }

    [Fact]
    public void DefenderCanTransferEvenAfterAllAttackersPassed()
    {
        var game = NewGame([
            [C(Suit.Clubs, Value.Six), C(Suit.Spades, Value.Seven)],
            [C(Suit.Diamonds, Value.Six)],
            [C(Suit.Clubs, Value.Seven), C(Suit.Diamonds, Value.Seven)],
        ]);
        game.PlayAttack(0, C(Suit.Clubs, Value.Six));
        Assert.Null(game.PassAttack(0));
        Assert.Null(game.PassAttack(2));

        Assert.Null(game.Transfer(1, C(Suit.Diamonds, Value.Six)));

        Assert.Equal(1, game.CurrentAttacker);
        Assert.Equal(2, game.CurrentDefender);
        Assert.Equal(2, game.CardsOnTable().Count);
    }

    [Fact]
    public void TransferIsNotAllowedAfterDefenseBegins()
    {
        var clubsSix = C(Suit.Clubs, Value.Six);

        var game = NewGame([
            [clubsSix, C(Suit.Diamonds, Value.Six)],
            [C(Suit.Clubs, Value.Seven), C(Suit.Diamonds, Value.Seven)],
            [C(Suit.Spades, Value.Seven), C(Suit.Hearts, Value.Seven)],
        ]);
        game.PlayAttack(0, clubsSix);
        Assert.Null(game.Defend(1, clubsSix, C(Suit.Clubs, Value.Seven)));

        Assert.Throws<InvalidOperationException>(() =>
            game.Transfer(1, C(Suit.Diamonds, Value.Seven)));
    }

    [Fact]
    public void TransferRequiresMatchingRankAndEnoughCardsForNextDefender()
    {
        var clubsSix = C(Suit.Clubs, Value.Six);

        var wrongRank = NewGame([
            [clubsSix],
            [C(Suit.Diamonds, Value.Seven)],
            [C(Suit.Clubs, Value.Seven), C(Suit.Diamonds, Value.Eight)],
        ]);
        wrongRank.PlayAttack(0, clubsSix);
        Assert.Throws<InvalidOperationException>(() =>
            wrongRank.Transfer(1, C(Suit.Diamonds, Value.Seven)));

        var tooFewCards = NewGame([
            [clubsSix],
            [C(Suit.Diamonds, Value.Six)],
            [C(Suit.Clubs, Value.Seven)],
        ]);
        tooFewCards.PlayAttack(0, clubsSix);
        Assert.Throws<InvalidOperationException>(() =>
            tooFewCards.Transfer(1, C(Suit.Diamonds, Value.Six)));
    }

    [Fact]
    public void TransfersCanChainAroundTheTable()
    {
        var game = NewGame([
            [
                C(Suit.Clubs, Value.Six),
                C(Suit.Clubs, Value.Seven),
                C(Suit.Clubs, Value.Eight),
                C(Suit.Clubs, Value.Nine),
            ],
            [C(Suit.Diamonds, Value.Six)],
            [C(Suit.Spades, Value.Six), C(Suit.Spades, Value.Seven)],
        ]);
        game.PlayAttack(0, C(Suit.Clubs, Value.Six));
        game.Transfer(1, C(Suit.Diamonds, Value.Six));

        Assert.Null(game.Transfer(2, C(Suit.Spades, Value.Six)));

        Assert.Equal(2, game.CurrentAttacker);
        Assert.Equal(0, game.CurrentDefender);
        Assert.Equal(3, game.CardsOnTable().Count);
    }

    [Fact]
    public void TransferKeepsMainAttackerForRoundResult()
    {
        var clubsSix = C(Suit.Clubs, Value.Six);

        var game = NewGame(
            [
                [clubsSix, C(Suit.Diamonds, Value.Seven)],
                [C(Suit.Diamonds, Value.Six)],
                [C(Suit.Hearts, Value.Seven), C(Suit.Hearts, Value.Eight)],
            ],
            talon: [C(Suit.Hearts, Value.Three), C(Suit.Hearts, Value.Four)]);
        game.PlayAttack(0, clubsSix);
        game.Transfer(1, C(Suit.Diamonds, Value.Six));
        Assert.Null(game.Defend(2, clubsSix, C(Suit.Hearts, Value.Seven)));
        Assert.Null(game.Defend(2, C(Suit.Diamonds, Value.Six), C(Suit.Hearts, Value.Eight)));

        var result = game.PassAttack(0);

        Assert.NotNull(result);
        Assert.Equal(0, result.Attacker);
        Assert.Equal(2, result.Defender);
        Assert.Equal(2, result.NextAttacker);
    }

    [Fact]
    public void MainAttackerDrawsFirstWhenTalonRunsOut()
    {
        var clubsSix = C(Suit.Clubs, Value.Six);
        var clubsSeven = C(Suit.Clubs, Value.Seven);

        var game = NewGame(
            [[clubsSix], [clubsSeven], [C(Suit.Diamonds, Value.Seven)]],
            talon: [C(Suit.Hearts, Value.Eight)]);
        game.PlayAttack(0, clubsSix);
        game.PassAttack(2);

        var result = game.Defend(1, clubsSix, clubsSeven);

        Assert.NotNull(result);
        Assert.Equal(new[] { 1, 0, 1 }, result.PlayerCardCounts);
        Assert.Equal(new[] { 1 }, result.FinishedPlayers);
        Assert.False(result.GameOver);
    }

    [Fact]
    public void GameEndsOnlyWhenOnePlayerStillHasCards()
    {
        var clubsSix = C(Suit.Clubs, Value.Six);
        var clubsSeven = C(Suit.Clubs, Value.Seven);

        var game = NewGame([[clubsSix], [C(Suit.Diamonds, Value.Six)], [clubsSeven]], talon: []);
        game.PlayAttack(0, clubsSix);
        Assert.Equal(new[] { 0 }, game.FinishedPlayers);
        Assert.False(game.GameOver);

        var firstRound = game.TakeCards(1);
        Assert.False(firstRound.GameOver);
        Assert.Equal(2, game.CurrentAttacker);
        Assert.Equal(1, game.CurrentDefender);

        game.PlayAttack(2, clubsSeven);
        var finalRound = game.TakeCards(1);

        Assert.True(finalRound.GameOver);
        Assert.Equal(new[] { 0, 2 }, finalRound.FinishedPlayers);
        Assert.Equal(new[] { 1, 3, 2 }, finalRound.PlayerPlacements);
        Assert.Equal(0, finalRound.GameWinner);
        Assert.Equal(1, finalRound.GameLoser);
        Assert.Throws<InvalidOperationException>(() =>
            game.PlayAttack(1, game.PlayerCards(1)[0]));
    }

    [Fact]
    public void PublicSnapshotsAreNotAffectedByLaterMoves()
    {
        var clubsSix = C(Suit.Clubs, Value.Six);

        var game = NewGame([[clubsSix, C(Suit.Diamonds, Value.Six)], [C(Suit.Clubs, Value.Seven)]]);
        var openingHand = game.PlayerCards(0);
        game.PlayAttack(0, clubsSix);
        Assert.Equal(2, openingHand.Count);

        var result = game.TakeCards(1);

        Assert.Single(result.CardsOnTable);
        Assert.Empty(game.CardsOnTable());
    }
}
