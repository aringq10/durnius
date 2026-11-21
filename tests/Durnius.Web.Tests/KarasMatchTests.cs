using Durnius.Web.Engines.Karas;
using Durnius.Web.Features.Multiplayer;
using Xunit;

namespace Durnius.Web.Tests;

public sealed class KarasMatchTests
{
    private static (KarasMatch Match, Response Response) Create() =>
        KarasMatch.Create(new MatchOptions([
            new MatchPlayer("p1", "Alice"),
            new MatchPlayer("p2", "Bob")
        ]));

    [Fact]
    public void CreateReturnsSeatedStartMessages()
    {
        var (match, response) = Create();

        Assert.Equal(2, match.Players.Count);
        Assert.Empty(response.Public);
        Assert.Equal(2, response.Private.Count);
        Assert.Equal("MatchStarted", response.Private["p1"].Method);
        Assert.Equal(1, Assert.IsType<MatchStart>(response.Private["p1"].Payload).You);
        Assert.Equal(2, Assert.IsType<MatchStart>(response.Private["p2"].Payload).You);
    }

    [Fact]
    public void CreateRejectsWrongPlayerCount()
    {
        Assert.Throws<ArgumentException>(() => KarasMatch.Create(
            new MatchOptions([new MatchPlayer("p1", "Alice")])));
    }

    [Fact]
    public void MoveFromUnknownPlayerIsIgnored()
    {
        var (match, _) = Create();

        var (ended, response) = match.MakeMove("stranger", new object());

        Assert.False(ended);
        Assert.Empty(response.Public);
        Assert.Empty(response.Private);
    }

    [Fact]
    public void DrawProducesStatusMessage()
    {
        var (match, _) = Create();

        var (ended, response) = match.MakeMove("p1", new object());

        Assert.False(ended);
        Assert.Contains(response.Public, m => m.Method == "DrawStatus");
    }

    [Fact]
    public void DisconnectEndsOnceAndLaterCallsAreNoOps()
    {
        var (match, _) = Create();

        var (ended, response) = match.Disconnect("p1");
        Assert.True(ended);
        Assert.Contains(response.Public, m => m.Method == "MatchEnded");
        Assert.Equal(2, Assert.IsType<int>(response.Public.Single().Payload));

        var (endedAfter, after) = match.MakeMove("p2", new object());
        Assert.False(endedAfter);
        Assert.Empty(after.Public);

        var (disconnectAfter, afterDisconnect) = match.Disconnect("p2");
        Assert.False(disconnectAfter);
        Assert.Empty(afterDisconnect.Public);
    }
}
