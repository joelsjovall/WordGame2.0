using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using WordGame.Api.Authentication;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Errors;
using WordGame.Api.Mapping;
using WordGame.Api.Synchronization;
using WordGame.Application.Lobbies;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Endpoints;

internal static class LobbyHostEndpoints
{
    internal static void MapLobbyHostEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/lobbies/{lobbyId:guid}").RequireAuthorization().WithTags("Lobbies");
        group.MapGet("", StateAsync).WithName("GetLobby").Produces<LobbyResponse>().Produces(401).Produces(404);
        group.MapGet("/events", EventsAsync).WithName("WatchLobby").Produces<LobbyResponse>(200, "text/event-stream").Produces(401).Produces(404);
        group.MapPost("/settings", ConfigureAsync).AddEndpointFilter<SameOriginWriteFilter>().WithName("ConfigureLobby")
            .Produces<LobbyResponse>().Produces(400).Produces(401).Produces(403).Produces(404).Produces(409);
        group.MapPost("/start", StartAsync).AddEndpointFilter<SameOriginWriteFilter>().WithName("StartLobby")
            .Produces<LobbyResponse>().Produces(400).Produces(401).Produces(403).Produces(404).Produces(409);
    }

    private static Guid Actor(HttpContext context) => Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static async Task<IResult> StateAsync(Guid lobbyId, HttpContext context, ILobbySessionStore store, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Vary = "Cookie";
        var lobby = await store.GetAsync(lobbyId, cancellationToken);
        return lobby?.Players.Any(player => player.Id == Actor(context)) == true
            ? TypedResults.Ok(LobbyMapper.Map(lobby)) : TypedResults.NotFound();
    }

    private static async Task<IResult> EventsAsync(Guid lobbyId, HttpContext context, ILobbySessionStore store, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var lobby = await store.GetAsync(lobbyId, cancellationToken);
        var actor = Actor(context);
        if (lobby?.Players.Any(player => player.Id == actor) != true)
        {
            return TypedResults.NotFound();
        }

        var identity = await context.AuthenticateAsync();
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Vary = "Cookie";
        context.Response.Headers["X-Accel-Buffering"] = "no";
        var expiry = identity.Properties?.ExpiresUtc ?? clock.GetUtcNow();
        return TypedResults.ServerSentEvents(LobbyEventStream.ReadAsync(lobby, actor, expiry, store, clock, cancellationToken));
    }

    private static Task<IResult> ConfigureAsync(Guid lobbyId, UpdateLobbySettingsRequest request, HttpContext context,
        LobbyHostService service, CancellationToken cancellationToken) => ExecuteAsync(context, async () =>
        await service.ConfigureAsync(lobbyId, Actor(context), Version(request.ExpectedVersion),
            new LobbySettings(request.Settings.MaximumPlayers, request.Settings.RoundCount, request.Settings.ChallengeSeconds,
                request.Settings.AllowedDifficulties.Select(value => Enum.Parse<CategoryDifficulty>(value.ToString()))), cancellationToken));

    private static Task<IResult> StartAsync(Guid lobbyId, StartLobbyRequest request, HttpContext context,
        LobbyHostService service, CancellationToken cancellationToken) => ExecuteAsync(context, () =>
            service.StartAsync(lobbyId, Actor(context), Version(request.ExpectedVersion), cancellationToken));

    private static long Version(string value) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var version) &&
        version >= 0 && value == version.ToString(CultureInfo.InvariantCulture)
        ? version : throw new ArgumentException("Provide a canonical nonnegative lobby version.");

    private static async Task<IResult> ExecuteAsync(HttpContext context, Func<Task<Lobby?>> action)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            var result = await action();
            return result is null ? TypedResults.NotFound() : TypedResults.Ok(LobbyMapper.Map(result));
        }
        catch (LobbyActionException exception)
        {
            return Problem(context, exception.Forbidden ? 403 : 409, exception.Message);
        }
        catch (ArgumentException exception) { return Problem(context, 400, exception.Message); }
    }

    private static IResult Problem(HttpContext context, int status, string title) =>
        TypedResults.Json(ApiProblems.Create(context, status, title), statusCode: status, contentType: "application/problem+json");
}
