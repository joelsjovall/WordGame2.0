namespace WordGame.Application.Sessions;

public sealed class SessionVersionConflictException() : Exception("The game state has changed; retrieve a fresh snapshot.");
