using System.Text.Json;
using Durnius.Web.Engines.Durnius;
using Durnius.Web.Features.Multiplayer;
using Xunit;

namespace Durnius.Web.Tests.Engines.Durnius;

public sealed class DurniusMatchTests
{
    private static readonly MatchPlayer Player1 = new("conn-1", "alice");
    private static readonly MatchPlayer Player2 = new("conn-2", "bob");

    private static MatchOptions Options() => new([Player1, Player2]);

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static DurniusStateDto State(Message message) =>
        Assert.IsType<DurniusStateDto>(message.Payload);

    [Fact]
    public void CreateSendsPrivateHandsAndTrumpToEachPlayer()
    {
        var (_, response) = DurniusMatch.Create(Options());

        Assert.Empty(response.Public);
        Assert.Equal(2, response.Private.Count);

        var first = State(response.Private[Player1.ConnectionId]);
        var second = State(response.Private[Player2.ConnectionId]);

        Assert.Equal(0, first.You);
        Assert.Equal(1, second.You);
        Assert.Equal(new[] { "alice", "bob" }, first.Players);
        Assert.Equal(new[] { 6, 6 }, first.CardCounts);
        Assert.Equal(6, first.Hand.Count);
        Assert.Equal(6, second.Hand.Count);
        Assert.Equal(first.TrumpSuit, first.TrumpCard.Suit);
        Assert.Equal(first.TrumpCard, second.TrumpCard);
        Assert.Equal(first.CurrentAttacker, second.CurrentAttacker);
        Assert.Equal(first.CurrentDefender, second.CurrentDefender);
    }

    [Fact]
    public void AttackAndDefendAdvanceTheGameForBothPlayers()
    {
        var (match, initial) = DurniusMatch.Create(Options());
        var opening = State(initial.Private[Player1.ConnectionId]);
        var attacker = opening.CurrentAttacker;
        var defender = (attacker + 1) % 2;
        var attackerConnection = attacker == 0 ? Player1.ConnectionId : Player2.ConnectionId;
        var defenderConnection = defender == 0 ? Player1.ConnectionId : Player2.ConnectionId;
        var hand = State(initial.Private[attackerConnection]).Hand;
        var card = hand[0];

        var move = JsonSerializer.Serialize(new
        {
            type = "attack",
            card = new { suit = card.Suit, value = card.Value }
        });
        var (result, afterAttack) = match.MakeMove(attackerConnection, Json(move));

        Assert.Null(result);
        Assert.Equal("GameState", afterAttack.Private[attackerConnection].Method);
        Assert.Single(State(afterAttack.Private[attackerConnection]).Table);
        Assert.Single(State(afterAttack.Private[defenderConnection]).Table);

        var table = State(afterAttack.Private[attackerConnection]).Table;
        Assert.Equal(attacker, table[0].PlayerIndex);
        Assert.Equal(card, table[0].AttackCard);

        // The defender has no matching beat for this card in a shuffled deal,
        // so taking the cards is always legal here.
        var (takeResult, afterTake) = match.MakeMove(defenderConnection, Json("""{"type":"take"}"""));

        Assert.Null(takeResult);
        var state = State(afterTake.Private[defenderConnection]);
        Assert.Empty(state.Table);
        Assert.NotNull(state.LastRound);
        Assert.True(state.LastRound.DefenderTookCards);
        Assert.Contains("RoundResult", afterTake.Public.Select(message => message.Method));
        Assert.Equal(state.LastRound.PlayerCardCounts, state.CardCounts);
    }

    [Fact]
    public void InvalidMovesDoNotChangeState()
    {
        var (match, initial) = DurniusMatch.Create(Options());
        var attacker = State(initial.Private[Player1.ConnectionId]).CurrentAttacker;
        var wrongPlayer = attacker == 0 ? Player2.ConnectionId : Player1.ConnectionId;

        var (result, response) = match.MakeMove(wrongPlayer, Json("""{"type":"attack","card":{"suit":"clubs","value":"six"}}"""));

        Assert.Null(result);
        Assert.Empty(response.Public);
        Assert.Empty(response.Private);

        var (unknownResult, unknownResponse) = match.MakeMove(Player1.ConnectionId, Json("""{"type":"dance"}"""));
        Assert.Null(unknownResult);
        Assert.Empty(unknownResponse.Public);
        Assert.Empty(unknownResponse.Private);
    }

    [Fact]
    public void DisconnectGivesTheRemainingPlayerFirstPlace()
    {
        var (match, _) = DurniusMatch.Create(Options());

        var (result, response) = match.Disconnect(Player1.ConnectionId);

        Assert.NotNull(result);
        Assert.Equal(Player2, result!.Placements.Single(placement => placement.Placement == 1).Player);
        Assert.Equal(Player1, result.Placements.Single(placement => placement.Placement == 2).Player);
        Assert.Equal(new[] { "MatchEnded", "MatchStandings" }, response.Public.Select(message => message.Method));
        Assert.Equal(1, response.Public[0].Payload);

        var (ignored, empty) = match.MakeMove(Player2.ConnectionId, Json("""{"type":"take"}"""));
        Assert.Null(ignored);
        Assert.Empty(empty.Public);
        Assert.Empty(empty.Private);
    }

    [Fact]
    public void AcceptsTypedMovesInAdditionToJson()
    {
        var (match, initial) = DurniusMatch.Create(Options());
        var attacker = State(initial.Private[Player1.ConnectionId]).CurrentAttacker;
        var connectionId = attacker == 0 ? Player1.ConnectionId : Player2.ConnectionId;
        var card = State(initial.Private[connectionId]).Hand[0];

        var move = new DurniusMove("attack", new DurniusCardDto(card.Suit, card.Value));

        var (_, response) = match.MakeMove(connectionId, move);

        Assert.Single(State(response.Private[connectionId]).Table);
    }
}
