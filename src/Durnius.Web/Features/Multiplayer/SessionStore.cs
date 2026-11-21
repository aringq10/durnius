using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Durnius.Web.Features.Multiplayer;

/// <summary>
/// Connection-scoped session registry. Removal is by exact instance so a stale
/// disconnect cannot evict a newer session for the same connection id.
/// </summary>
public sealed class SessionStore
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    public Session Add(Session session)
    {
        if (!_sessions.TryAdd(session.ConnectionId, session))
        {
            throw new InvalidOperationException(
                $"Session for connection '{session.ConnectionId}' already exists.");
        }

        return session;
    }

    public Session? TryGet(string connectionId) =>
        _sessions.TryGetValue(connectionId, out var session) ? session : null;

    public Session Get(string connectionId) =>
        TryGet(connectionId) ?? throw new HubException("Unknown session.");

    public Session Get(HubCallerContext context) => Get(context.ConnectionId);

    public bool Remove(Session session) =>
        _sessions.TryRemove(new KeyValuePair<string, Session>(session.ConnectionId, session));

    public int Count => _sessions.Count;
}
