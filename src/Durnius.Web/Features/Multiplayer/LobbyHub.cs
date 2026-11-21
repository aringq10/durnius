using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Durnius.Web.Features.Multiplayer;

/// <summary>
/// Transport only: coordination state lives in the session store and managers.
/// </summary>
[Authorize]
public class LobbyHub(
    SessionStore sessions,
    LobbyManager lobby,
    MatchManager match,
    MultiplayerCoordinator coordinator,
    ILogger<LobbyHub> logger) : Hub
{
    private readonly SessionStore _sessions = sessions;
    private readonly LobbyManager _lobby = lobby;
    private readonly MatchManager _match = match;
    private readonly MultiplayerCoordinator _coordinator = coordinator;
    private readonly ILogger<LobbyHub> _logger = logger;

    public override Task OnConnectedAsync()
    {
        _sessions.Add(new Session(Context.ConnectionId, Context.User!.Identity!.Name!));
        return base.OnConnectedAsync();
    }

    public async Task JoinLobby(string lobby)
    {
        var result = await _lobby.JoinAsync(_sessions.Get(Context), lobby);

        if (result.Rejection is { } rejection)
        {
            await Clients.Caller.SendAsync("JoinRejected", rejection);
            return;
        }

        await Apply(result.Effects!);
    }

    public async Task LeaveLobby()
    {
        var effects = await _lobby.LeaveAsync(_sessions.Get(Context));
        if (effects is not null)
        {
            await Apply(effects);
        }
    }

    public async Task MakeMove(object move)
    {
        var effects = await _match.ForwardMoveAsync(_sessions.Get(Context), move);
        if (effects is not null)
        {
            await Apply(effects);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var session = _sessions.TryGet(Context.ConnectionId);
        if (session is not null)
        {
            var effects = await _coordinator.DisconnectAsync(session);

            if (effects is not null)
            {
                await Apply(effects);
            }

            _sessions.Remove(session);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task Apply(CoordinationEffects effects)
    {
        foreach (var effect in effects.Items)
        {
            try
            {
                switch (effect)
                {
                    case AddToGroupEffect add:
                        await Groups.AddToGroupAsync(add.ConnectionId, add.Group);
                        break;
                    case RemoveFromGroupEffect remove:
                        await Groups.RemoveFromGroupAsync(remove.ConnectionId, remove.Group);
                        break;
                    case LobbyUpdateEffect update:
                        await Clients.Group(update.LobbyName).SendAsync("LobbyUpdate", update.Update);
                        break;
                    case ResponseEffect response:
                        await SendResponse(response);
                        break;
                }
            }
            catch (Exception ex)
            {
                // Sends happen after state is committed; a failed send must not
                // roll back (or corrupt) coordination state.
                _logger.LogError(ex, "Failed to apply SignalR effect {Effect}.", effect);
            }
        }
    }

    private async Task SendResponse(ResponseEffect effect)
    {
        foreach (var message in effect.Response.Public)
        {
            await Clients.Group(effect.MatchId.ToString()).SendAsync(message.Method, message.Payload);
        }
        foreach (var (connectionId, message) in effect.Response.Private)
        {
            await Clients.Client(connectionId).SendAsync(message.Method, message.Payload);
        }
    }
}
