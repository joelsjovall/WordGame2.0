namespace WordGame.Api.Contracts.V1;

public sealed record SpectatorStateResponse(string LobbyStatus, GameStateResponse? Game);
