namespace Durnius.Web.Features.Multiplayer;

public static class RoomExtensions
{
    /// <summary>
    /// Runs <paramref name="action"/> under the gate of the session's current
    /// room (when it is a <typeparamref name="TRoom"/>) and the session's own
    /// ownership gate. Returns <c>null</c> if the session is not in such a room.
    /// </summary>
    /// <remarks>
    /// Lock order: room gate first, session gate second. Holding the session
    /// gate makes the room reference stable for the duration of the action, so
    /// ownership cannot be transferred by another room while it runs. The
    /// identity recheck handles a room change while waiting for the room gate.
    /// </remarks>
    public static async Task<TResult?> RunInRoomAsync<TRoom, TResult>(
        this Session session,
        Func<TRoom, Task<TResult>> action)
        where TRoom : Room
        where TResult : class
    {
        while (true)
        {
            if (session.Room is not TRoom room)
            {
                return null;
            }

            await room.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await session.Gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (!ReferenceEquals(session.Room, room))
                    {
                        continue; // moved while waiting; release both gates and retry
                    }

                    return await action(room).ConfigureAwait(false);
                }
                finally
                {
                    session.Gate.Release();
                }
            }
            finally
            {
                room.Gate.Release();
            }
        }
    }
}
