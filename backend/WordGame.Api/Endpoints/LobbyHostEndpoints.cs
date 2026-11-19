using Microsoft.AspNetCore.Authentication;
using WordGame.Api.Authentication;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Mapping;
using WordGame.Api.Synchronization;
using WordGame.Application.Lobbies;
using WordGame.Application.Sessions;
using WordGame.Domain;
using static WordGame.Api.Endpoints.LobbyRequests;

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
        group.MapPost("/rematch", RematchAsync).AddEndpointFilter<SameOriginWriteFilter>().WithName("PreparePartyRematch")
            .Produces<LobbyResponse>().Produces(400).Produces(401).Produces(403).Produces(404).Produces(409);
    }

    private static async Task<IResult> StateAsync(Guid lobbyId, HttpContext context, ILobbySessionStore store, TimeProvider clock, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Vary = "Cookie";
        var lobby = await store.GetAsync(lobbyId, cancellationToken);
        return lobby?.Players.Any(player => player.Id == Actor(context)) == true
            ? TypedResults.Ok(LobbyMapper.Map(lobby, clock.GetUtcNow())) : TypedResults.NotFound();
    }

    private static async Task<IResult> EventsAsync(Guid lobbyId, HttpContext context, ILobbySessionStore store, LobbyPlayerService players, TimeProvider clock,
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
        var session = PlayerAuthentication.Session(context);
        return session is null ? TypedResults.Unauthorized()
            : TypedResults.ServerSentEvents(LobbyEventStream.ReadAsync(lobby, actor, expiry, store, players, clock, session.Revoked, cancellationToken));
    }

    private static Task<IResult> ConfigureAsync(Guid lobbyId, UpdateLobbySettingsRequest request, HttpContext context,
        LobbyHostService service, CancellationToken cancellationToken) => ExecuteAsync(context, async () =>
        await service.ConfigureAsync(lobbyId, Actor(context), Version(request.ExpectedVersion),
            new LobbySettings(request.Settings.MaximumPlayers, request.Settings.RoundCount, request.Settings.ChallengeSeconds,
                request.Settings.AllowedDifficulties.Select(value => Enum.Parse<CategoryDifficulty>(value.ToString()))), cancellationToken));

    private static Task<IResult> StartAsync(Guid lobbyId, StartLobbyRequest request, HttpContext context,
        LobbyHostService service, CancellationToken cancellationToken) => ExecuteAsync(context, () =>
            service.StartAsync(lobbyId, Actor(context), Version(request.ExpectedVersion), cancellationToken));

    private static Task<IResult> RematchAsync(Guid lobbyId, StartLobbyRequest request, HttpContext context,
        LobbyHostService service, CancellationToken cancellationToken) => ExecuteAsync(context, () =>
            service.RematchAsync(lobbyId, Actor(context), Version(request.ExpectedVersion), cancellationToken));

}
