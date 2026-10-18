using Durnius.Web.Engines.Karas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Durnius.Web.Features.Multiplayer;

public interface ILobbyClient
{
    Task JoinRejected(string reason);

    Task LobbyUpdate(object state);

    Task MatchStarted(object state);

    Task MatchEnded(int winner);

    Task DrawStatus(object state);

    Task RoundResult(object state);
}

[Authorize]
public class LobbyHub : Hub<ILobbyClient>
{
    private readonly LobbyManager _lobby;
    private readonly MatchManager _match;
    private string Username => Context.User!.Identity!.Name!;

    public LobbyHub(LobbyManager lobby, MatchManager match)
    {
        _lobby = lobby;
        _match = match;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var r = _match.End(Context.ConnectionId);
        if (r is (var opponent, var winner))
        {
            await Clients.Client(opponent.ConnectionId).MatchEnded(winner);
        }

        await LeaveLobby();

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinLobby()
    {
        if (_match.IsRunning)
        {
            await Clients.Caller.JoinRejected("Ongoing match");
            return;
        }

        var (State, StartMatch) = _lobby.Join(Context.ConnectionId, Username);
        if (State is null)
        {
            await Clients.Caller.JoinRejected("Already in this lobby");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "Lobby");

        await Clients.Group("Lobby").LobbyUpdate(LobbyDto(State));

        if (StartMatch)
        {
            await TryStartMatchAsync(State.Players);
        }
    }

    public async Task LeaveLobby()
    {
        var removed = _lobby.Remove(Context.ConnectionId);
        if (removed)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Lobby");
            await Clients.Group("Lobby").LobbyUpdate(LobbyDto(_lobby.GetState()));
        }
    }

    /// <summary>Marks this player's draw for the round; resolves the round once both have drawn.</summary>
    public async Task Draw()
    {
        var outcome = _match.Draw(Context.ConnectionId);
        if (outcome is null)
            return;

        var connections = new[] { outcome.Player1.ConnectionId, outcome.Player2.ConnectionId };
        await Clients.Clients(connections).DrawStatus(new
        {
            player1Drawn = outcome.Player1Drawn,
            player2Drawn = outcome.Player2Drawn,
        });

        if (outcome.Round is not null)
        {
            await Clients.Clients(connections).RoundResult(RoundDto(outcome.Round));
        }

        if (outcome.MatchEnded)
        {
            await Clients.Clients(connections).MatchEnded(outcome.Round!.GameWinner);
        }
    }

    private async Task TryStartMatchAsync(IReadOnlyList<MatchPlayer> players)
    {
        var start = _match.TryStart(players);
        if (start is null)
            return;

        await Clients.Client(start.Player1.ConnectionId).MatchStarted(new
        {
            you = 1,
            player1 = start.Player1.Username,
            player2 = start.Player2.Username,
            player1Count = start.Player1Count,
            player2Count = start.Player2Count,
        });
        await Clients.Client(start.Player2.ConnectionId).MatchStarted(new
        {
            you = 2,
            player1 = start.Player1.Username,
            player2 = start.Player2.Username,
            player1Count = start.Player1Count,
            player2Count = start.Player2Count,
        });
    }

    private static object RoundDto(RoundResult round) => new
    {
        roundWinner = round.RoundWinner,
        player1Card = CardDto(round.Player1Card),
        player2Card = CardDto(round.Player2Card),
        war = round.War.Select(w => new { player1 = CardDto(w.Player1Card), player2 = CardDto(w.Player2Card) }).ToArray(),
        player1Count = round.Player1Count,
        player2Count = round.Player2Count,
        gameOver = round.GameOver,
        gameWinner = round.GameWinner,
    };

    private static object? CardDto(Card card) => new
        { suit = card.Suit.ToString().ToLowerInvariant(), value = card.Value.ToString().ToLowerInvariant() };

    private static object LobbyDto(LobbyState state) => new
    {
        capacity = state.Capacity,
        count = state.Count,
        players = state.Players.Select(player => player.Username).ToList()
    };
}
