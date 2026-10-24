using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Durnius.Web.Features.Multiplayer;

[Authorize]
public class LobbyHub(LobbyManager lobby, MatchManager match) : Hub
{
    private readonly LobbyManager _lobby = lobby;
    private readonly MatchManager _match = match;
    private string Username => Context.User!.Identity!.Name!;
    private Guid? MatchId
    {
        get => _match.GetByConnection(Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (MatchId is { } id)
        {
            var (response, ended, players) = _match.ForwardsDisconnect(id, Context.ConnectionId);
            await SendResponse(id, response);
            if (ended && players is not null)
            {
                await _match.CleanupMatch(id, players);
            }
        }

        await LeaveLobby();

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinLobby()
    {
        var (State, StartMatch) = _lobby.Join(Context.ConnectionId, Username);
        if (State is null)
        {
            await Clients.Caller.SendAsync("JoinRejected", "Already in this lobby");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "Lobby");
        await Clients.Group("Lobby").SendAsync("LobbyUpdate", new LobbyUpdateDto(State));

        if (StartMatch)
        {
            foreach (var p in State.Players)
            {
                await Groups.RemoveFromGroupAsync(p.ConnectionId, "Lobby");
            }

            var r = await _match.StartNewMatch(Game.Karas, new(State.Players));

            await SendResponse(r.Guid, r.Response);
        }
    }

    public async Task LeaveLobby()
    {
        var state = _lobby.Remove(Context.ConnectionId);
        if (state is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Lobby");
            await Clients.Group("Lobby").SendAsync("LobbyUpdate", new LobbyUpdateDto(state));
        }
    }

    public async Task MakeMove(object move)
    {
        if (MatchId is { } id)
        {
            var (response, ended, players) = _match.ForwardMove(id, Context.ConnectionId, move);
            await SendResponse(id, response);
            if (ended && players is not null)
            {
                await _match.CleanupMatch(id, players);
            }
        }
    }

    private async Task SendResponse(Guid guid, Response? response)
    {
        if (response is not null)
        {
            foreach (var m in response.Public)
            {
                await Clients.Group(guid.ToString()).SendAsync(m.Method, m.Payload);
            }
            foreach (var m in response.Private)
            {
                await Clients.Client(m.Key).SendAsync(m.Value.Method, m.Value.Payload);
            }
        }
    }
}

public record LobbyUpdateDto
{
    public int Capacity { get; init; }
    public int Count { get; init; }
    public IReadOnlyList<string> Players { get; init; }

    public LobbyUpdateDto(LobbyState state)
    {
        Capacity = state.Capacity;
        Count = state.Count;
        Players = state.Players.Select(player => player.Username).ToList();
    }
};
