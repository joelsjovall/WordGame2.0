namespace WordGame.Application.Sessions;

// Public player IDs are identifiers, never credentials. Only the protected browser cookie carries the session ID.
public sealed record GuestSession(Guid Id, Guid PlayerId, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, CancellationToken Revoked);

public interface IGuestSessionStore
{
    GuestSession Issue(Guid playerId);
    GuestSession? Get(Guid sessionId);
    void Revoke(Guid sessionId);
}
