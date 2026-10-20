namespace AmusementRideCRM.Services;

using System.Collections.Concurrent;

public class QBSession
{
    public string Ticket { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CurrentSyncId { get; set; }
    public string LastError { get; set; } = string.Empty;
}

public interface IQBSessionManager
{
    string CreateSession(string username);
    bool ValidateSession(string ticket);
    QBSession? GetSession(string ticket);
    void SetCurrentSyncId(string ticket, int syncId);
    void SetLastError(string ticket, string error);
    void CloseSession(string ticket);
}

public class QBSessionManager : IQBSessionManager
{
    private readonly ConcurrentDictionary<string, QBSession> _sessions = new();

    public string CreateSession(string username)
    {
        var ticket = Guid.NewGuid().ToString();
        var session = new QBSession
        {
            Ticket = ticket,
            Username = username,
            CreatedAt = DateTime.UtcNow
        };
        _sessions[ticket] = session;
        return ticket;
    }

    public bool ValidateSession(string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket)) return false;
        return _sessions.TryGetValue(ticket, out var session) && session.CreatedAt > DateTime.UtcNow.AddHours(-24);
    }

    public QBSession? GetSession(string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket)) return null;
        _sessions.TryGetValue(ticket, out var session);
        return session;
    }

    public void SetCurrentSyncId(string ticket, int syncId)
    {
        var session = GetSession(ticket);
        if (session != null)
        {
            session.CurrentSyncId = syncId;
        }
    }

    public void SetLastError(string ticket, string error)
    {
        var session = GetSession(ticket);
        if (session != null)
        {
            session.LastError = error;
        }
    }

    public void CloseSession(string ticket)
    {
        if (!string.IsNullOrWhiteSpace(ticket))
        {
            _sessions.TryRemove(ticket, out _);
        }
    }
}
