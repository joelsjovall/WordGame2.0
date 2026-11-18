namespace WordGame.Application.Sessions;

public enum SessionChangeKind
{
    Updated,
    Removed
}

// An invalidation signal: consumers retrieve a fresh authorized snapshot.
public sealed record SessionChange(Guid SessionId, long Version, SessionStatus Status, SessionChangeKind Kind);
