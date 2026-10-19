using Durnius.Web.Engines.Karas;

namespace Durnius.Web.Features.Multiplayer;

public static class MultiplayerDtoExtensions
{
    public static object ToDto(this Card card) => new
    {
        suit = card.Suit.ToString().ToLowerInvariant(),
        value = card.Value.ToString().ToLowerInvariant()
    };

    public static object ToDto(this RoundResult round) => new
    {
        roundWinner = round.RoundWinner,
        player1Card = round.Player1Card.ToDto(),
        player2Card = round.Player2Card.ToDto(),
        war = round.War.Select(w => new { player1 = w.Player1Card.ToDto(), player2 = w.Player2Card.ToDto() }).ToArray(),
        player1Count = round.Player1Count,
        player2Count = round.Player2Count,
        gameOver = round.GameOver,
        gameWinner = round.GameWinner,
    };

    public static object ToDto(this LobbyState state) => new
    {
        capacity = state.Capacity,
        count = state.Count,
        players = state.Players.Select(player => player.Username).ToList()
    };
}
