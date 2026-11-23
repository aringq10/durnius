using Durnius.Web.Engines;
using Durnius.Web.Engines.Karas;
using Durnius.Web.Features.Multiplayer;
using Xunit;

namespace Durnius.Web.Tests.Engines.Karas;

public class KarasMatchTests
{
    private static Card C(Suit suit, Value value) => new(suit, value);

    private static MatchOptions Options() => new([
        new MatchPlayer("p1", "Alice"),
        new MatchPlayer("p2", "Bob")
    ]);

    [Fact]
    public void CreateRequiresExactlyTwoPlayers()
    {
        Assert.Throws<ArgumentException>(() => KarasMatch.Create(new MatchOptions([
            new MatchPlayer("p1", "Alice")
        ])));
        Assert.Throws<ArgumentException>(() => KarasMatch.Create(new MatchOptions([
            new MatchPlayer("p1", "Alice"),
            new MatchPlayer("p2", "Bob"),
            new MatchPlayer("p3", "Carol")
        ])));
    }

    [Fact]
    public void CreateSendsPrivateStartMessagesWithBothPlayerIndices()
    {
        var players = Options().Players;

        var (_, response) = KarasMatch.Create(new MatchOptions(players));

        Assert.Empty(response.Public);
        Assert.Equal(2, response.Private.Count);
        var first = Assert.IsType<MatchStart>(response.Private["p1"].Payload);
        var second = Assert.IsType<MatchStart>(response.Private["p2"].Payload);
        Assert.Equal("MatchStarted", response.Private["p1"].Method);
        Assert.Equal(1, first.You);
        Assert.Equal(2, second.You);
        Assert.Equal("Alice", first.Player1);
        Assert.Equal("Bob", first.Player2);
        Assert.Equal(26, first.Player1Count);
        Assert.Equal(26, first.Player2Count);
        Assert.Equal(first.Player1Count, second.Player1Count);
    }

    [Fact]
    public void UnknownConnectionAndRepeatedDrawDoNothing()
    {
        var (match, _) = KarasMatch.Create(Options());

        var stranger = match.MakeMove("stranger", new object());
        Assert.Null(stranger.Result);
        Assert.Empty(stranger.Response.Public);

        var first = match.MakeMove("p1", new object());
        var status = Assert.IsType<DrawStatus>(Assert.Single(first.Response.Public).Payload);
        Assert.True(status.player1Drawn);
        Assert.False(status.player2Drawn);

        var duplicate = match.MakeMove("p1", new object());
        Assert.Null(duplicate.Result);
        Assert.Empty(duplicate.Response.Public);

        var second = match.MakeMove("p2", new object());
        Assert.Equal(2, second.Response.Public.Count);
        Assert.Equal("DrawStatus", second.Response.Public[0].Method);
        Assert.Equal("RoundResult", second.Response.Public[1].Method);
        var bothDrawn = Assert.IsType<DrawStatus>(second.Response.Public[0].Payload);
        Assert.True(bothDrawn.player1Drawn);
        Assert.True(bothDrawn.player2Drawn);
    }

    [Fact]
    public void RoundResultDtoIncludesWarExchanges()
    {
        var engine = new GameEngine(
            [C(Suit.Hearts, Value.Seven), C(Suit.Hearts, Value.Two), C(Suit.Hearts, Value.Three)],
            [C(Suit.Spades, Value.Seven), C(Suit.Clubs, Value.Two), C(Suit.Clubs, Value.Four)]);
        var match = new KarasMatch(new MatchPlayer("p1", "Alice"), new MatchPlayer("p2", "Bob"), engine);

        match.MakeMove("p1", new object());
        var (_, response) = match.MakeMove("p2", new object());

        var dto = Assert.IsType<RoundResultDto>(response.Public[1].Payload);
        Assert.Equal(2, dto.War.Length);
        Assert.Equal("two", dto.War[0].Player1.Value);
        Assert.Equal("two", dto.War[0].Player2.Value);
        Assert.Equal(Value.Three.ToString().ToLowerInvariant(), dto.War[1].Player1.Value);
        Assert.Equal("four", dto.War[1].Player2.Value);
        Assert.Equal("hearts", dto.Player1Card.Suit);
        Assert.Equal(2, dto.RoundWinner);
    }

    [Fact]
    public void GameOverSendsMatchEndedAndFinalPlacements()
    {
        var engine = new GameEngine([C(Suit.Hearts, Value.Two)], [C(Suit.Spades, Value.Three)]);
        var match = new KarasMatch(new MatchPlayer("p1", "Alice"), new MatchPlayer("p2", "Bob"), engine);

        var first = match.MakeMove("p1", new object());
        Assert.Null(first.Result);
        Assert.Single(first.Response.Public);

        var (result, response) = match.MakeMove("p2", new object());

        Assert.Equal(3, response.Public.Count);
        var dto = Assert.IsType<RoundResultDto>(response.Public[1].Payload);
        Assert.Equal("hearts", dto.Player1Card.Suit);
        Assert.Equal("two", dto.Player1Card.Value);
        Assert.Equal("spades", dto.Player2Card.Suit);
        Assert.Equal("three", dto.Player2Card.Value);
        Assert.Equal(0, dto.Player1Count);
        Assert.Equal(2, dto.Player2Count);
        Assert.True(dto.GameOver);
        Assert.Equal(2, dto.GameWinner);
        Assert.Empty(dto.War);
        Assert.Equal("MatchEnded", response.Public[2].Method);
        Assert.Equal(2, response.Public[2].Payload);

        Assert.NotNull(result);
        Assert.Equal("Bob", result.Placements[0].Player.Username);
        Assert.Equal(1, result.Placements[0].Placement);
        Assert.Equal("Alice", result.Placements[1].Player.Username);
        Assert.Equal(2, result.Placements[1].Placement);

        var afterEnd = match.MakeMove("p1", new object());
        Assert.Null(afterEnd.Result);
        Assert.Empty(afterEnd.Response.Public);
    }

    [Fact]
    public void DisconnectGivesTheRemainingPlayerFirstPlaceAndIsIdempotent()
    {
        var (match, _) = KarasMatch.Create(Options());

        var (result, response) = match.Disconnect("p1");

        Assert.NotNull(result);
        Assert.Equal("Bob", result.Placements[0].Player.Username);
        Assert.Equal(1, result.Placements[0].Placement);
        Assert.Equal("Alice", result.Placements[1].Player.Username);
        Assert.Equal(2, result.Placements[1].Placement);
        Assert.Equal(2, Assert.Single(response.Public).Payload);

        Assert.Null(match.Disconnect("p1").Result);
        Assert.Null(match.Disconnect("p2").Result);
        Assert.Null(match.MakeMove("p2", new object()).Result);
        Assert.Empty(match.MakeMove("p2", new object()).Response.Public);
    }
}
