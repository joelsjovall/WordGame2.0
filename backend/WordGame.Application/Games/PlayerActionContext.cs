namespace WordGame.Application.Games;

// Supplied by an authenticated adapter; revision is checked atomically by the store.
public sealed record PlayerActionContext(Guid ActionId, long ExpectedVersion);
