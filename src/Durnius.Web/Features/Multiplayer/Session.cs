namespace Durnius.Web.Features.Multiplayer;

/// <summary>
/// A connected client and its current room membership.
/// </summary>
/// <remarks>
/// <see cref="Gate"/> is the session-ownership primitive. A session belongs to
/// at most one room, and a room change is only valid while holding both the
/// target/source room's gate and this gate. Lock order is always
/// <c>Room.Gate</c> then <c>Session.Gate</c>; the session gate is a leaf.
/// </remarks>
public sealed class Session(string connectionId, string username)
{
    public string ConnectionId { get; } = connectionId;
    public string Username { get; } = username;

    internal SemaphoreSlim Gate { get; } = new(1, 1);

    private volatile Room? _room;

    internal Room? Room
    {
        get => _room;
        set => _room = value; // only while holding the room gate and this gate
    }
}
