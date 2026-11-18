using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Mapping;
using WordGame.Api.Synchronization;
using WordGame.Application.Games;
using WordGame.Application.Synchronization;

namespace WordGame.Api.Endpoints;

internal static class GameSynchronizationEndpoints
{
    internal static void MapGameSynchronizationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/games/{gameId:guid}").RequireAuthorization().WithTags("Games");
        group.MapGet("/state", StateAsync).WithName("GetGameState")
            .Produces<GameStateResponse>().Produces(401).Produces(404);
        group.MapGet("/events", EventsAsync).WithName("WatchGameEvents")
            .Produces<GameEventResponse>(200, "text/event-stream").Produces(401).Produces(404);
    }

    private static async Task<IResult> StateAsync(Guid gameId, HttpContext context, GameEventService events, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var playerId))
        {
            return TypedResults.Unauthorized();
        }

        var state = await events.GetStateAsync(gameId, playerId, cancellationToken);
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Vary = "Cookie";
        return state is null ? TypedResults.NotFound() : TypedResults.Ok(GameStateMapper.Map(state, clock.GetUtcNow()));
    }

    private static async Task<IResult> EventsAsync(Guid gameId, HttpContext context, GameEventService events, GameEngine engine,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var playerId))
        {
            return TypedResults.Unauthorized();
        }

        var state = await events.GetStateAsync(gameId, playerId, cancellationToken);
        if (state is null)
        {
            return TypedResults.NotFound();
        }

        var identity = await context.AuthenticateAsync();
        var expires = identity.Properties?.ExpiresUtc ?? clock.GetUtcNow().AddDays(1);
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Vary = "Cookie";
        context.Response.Headers["X-Accel-Buffering"] = "no";
        return TypedResults.ServerSentEvents(GameEventStream.ReadAsync(state, playerId, expires, events, engine, clock, cancellationToken));
    }
}
