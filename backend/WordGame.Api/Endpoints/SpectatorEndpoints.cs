using System.Security.Cryptography;
using System.Text;
using WordGame.Api.Authentication;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Mapping;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Endpoints;

internal static class SpectatorEndpoints
{
    internal static void MapSpectatorEndpoints(this WebApplication app) =>
        app.MapGet("/api/v1/lobbies/{lobbyId:guid}/spectate/{code}", StateAsync)
            .RequireRateLimiting(LobbyRateLimiting.MatchmakingRead)
            .WithTags("Lobbies").WithName("GetSpectatorState")
            .Produces<SpectatorStateResponse>().Produces(404).Produces(429);

    private static async Task<IResult> StateAsync(Guid lobbyId, string code, HttpContext context,
        ILobbySessionStore lobbies, GameEngine games, TimeProvider clock, SessionRetention retention,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var lobby = await lobbies.GetAsync(lobbyId, cancellationToken);
        var provided = Encoding.ASCII.GetBytes(code.ToUpperInvariant());
        if (lobby is null || provided.Length != LobbyCode.Length ||
            !CryptographicOperations.FixedTimeEquals(provided, Encoding.ASCII.GetBytes(lobby.Code.Value)))
        {
            return TypedResults.NotFound();
        }

        GameStateResponse? game = null;
        if (lobby.GameId is { } gameId)
        {
            var state = await games.GetStateAsync(gameId, cancellationToken);
            if (state.Succeeded)
            {
                game = GameStateMapper.Map(state.State!, clock.GetUtcNow(), retention.GraceFor(state.State!.Game.Settings.Format));
            }
        }

        return TypedResults.Ok(new SpectatorStateResponse(lobby.Status.ToString().ToLowerInvariant(), game));
    }
}
