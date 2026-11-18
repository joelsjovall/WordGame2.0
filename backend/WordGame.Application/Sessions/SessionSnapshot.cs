using WordGame.Domain;

namespace WordGame.Application.Sessions;

// Game is a detached copy, never the mutable instance held by a store.
public sealed record SessionSnapshot(
    Guid Id,
    SessionStatus Status,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? EndedAt,
    IReadOnlyList<PlayerPresence> Players,
    Game Game);

public sealed record PlayerPresence(Guid PlayerId, int ConnectionCount, DateTimeOffset? DisconnectedAt, bool HasLeft = false);
