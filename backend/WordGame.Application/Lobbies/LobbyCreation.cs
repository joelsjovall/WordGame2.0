using WordGame.Domain;

namespace WordGame.Application.Lobbies;

public sealed record LobbyCreation(Lobby Lobby, bool WasCreated);

public sealed class LobbyCreationConflictException() : Exception("This request ID was already used for a different lobby creation.");
