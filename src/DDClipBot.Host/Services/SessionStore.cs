using System.Collections.Concurrent;
using DDClipBot.Host.Models;

namespace DDClipBot.Host.Services;

public interface ISessionStore
{
    void CreateSession(UserSession session);
    UserSession? GetSession(string sessionId);
    void RemoveSession(string sessionId);
}

public class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, UserSession> _sessions = new();

    public void CreateSession(UserSession session)
    {
        _sessions[session.SessionId] = session;
    }

    public UserSession? GetSession(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    public void RemoveSession(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }
}
