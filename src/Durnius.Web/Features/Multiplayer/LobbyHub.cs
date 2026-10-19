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
public class LobbyHub(LobbyManager lobby, MatchManager match) : Hub<ILobbyClient>
{
    private readonly LobbyManager _lobby = lobby;
    private readonly MatchManager _match = match;
    private string Username => Context.User!.Identity!.Name!;

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

        await Clients.Group("Lobby").LobbyUpdate(State.ToDto());

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
            await Clients.Group("Lobby").LobbyUpdate(_lobby.GetState().ToDto());
        }
    }

    /// <summary>Marks this player's draw for the round; resolves the round once both have drawn.</summary>
    public async Task Draw()
    {
        var outcome = _match.Draw(Context.ConnectionId);
        if (outcome is null)
        {
            return;
        }

        var connections = new[] { outcome.Player1.ConnectionId, outcome.Player2.ConnectionId };
        await Clients.Clients(connections).DrawStatus(new
        {
            player1Drawn = outcome.Player1Drawn,
            player2Drawn = outcome.Player2Drawn,
        });

        if (outcome.Round is not null)
        {
            await Clients.Clients(connections).RoundResult(outcome.Round.ToDto());
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
        {
            return;
        }

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
}
