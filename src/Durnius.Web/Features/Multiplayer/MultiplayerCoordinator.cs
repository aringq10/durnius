namespace Durnius.Web.Features.Multiplayer;

/// <summary>
/// Dispatches a disconnect to whichever room currently owns the session.
/// The room gate is taken before the session gate, so the room cannot change
/// while the disconnect is being applied.
/// </summary>
public sealed class MultiplayerCoordinator(LobbyManager lobby, MatchManager matches)
{
    public Task<CoordinationEffects?> DisconnectAsync(Session session) =>
        session.RunInRoomAsync<Room, CoordinationEffects>(room => room switch
        {
            LobbyRoom lobbyRoom => lobby.LeaveInRoomAsync(session, lobbyRoom),
            MatchRoom matchRoom => matches.DisconnectInRoomAsync(session, matchRoom),
            _ => Task.FromResult(new CoordinationEffects())
        });
}
