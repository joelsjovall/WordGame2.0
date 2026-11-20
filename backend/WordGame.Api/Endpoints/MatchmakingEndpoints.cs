using WordGame.Api.Authentication;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Errors;
using WordGame.Api.Synchronization;
using WordGame.Application.Matchmaking;
using WordGame.Application.Sessions;

namespace WordGame.Api.Endpoints;

internal static class MatchmakingEndpoints
{
    internal static void MapMatchmakingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/matchmaking").RequireAuthorization().WithTags("Matchmaking")
            .RequireRateLimiting(LobbyRateLimiting.MatchmakingRead);
        group.MapPost("/searches", JoinAsync).AddEndpointFilter<SameOriginWriteFilter>()
            .RequireRateLimiting(LobbyRateLimiting.Matchmaking).WithName("JoinMatchmaking")
            .Produces<MatchmakingResponse>().Produces(400).Produces(401).Produces(403).Produces(409).Produces(429);
        group.MapGet("/status", StatusAsync).WithName("GetMatchmakingStatus").Produces<MatchmakingResponse>().Produces(401).Produces(429);
        group.MapGet("/events", EventsAsync).WithName("WatchMatchmaking")
            .Produces<MatchmakingResponse>(200, "text/event-stream").Produces(401).Produces(429);
        group.MapGet("/searches/{searchId:guid}", SearchAsync).WithName("GetMatchmakingSearch")
            .Produces<MatchmakingResponse>().Produces(401).Produces(404).Produces(429);
        group.MapDelete("/searches/{searchId:guid}", CancelAsync).AddEndpointFilter<SameOriginWriteFilter>()
            .RequireRateLimiting(LobbyRateLimiting.Matchmaking)
            .WithName("CancelMatchmaking").Produces(204).Produces(401).Produces(403).Produces(404).Produces(409).Produces(429);
        group.MapPost("/searches/{searchId:guid}/accept", AcceptAsync).AddEndpointFilter<SameOriginWriteFilter>()
            .RequireRateLimiting(LobbyRateLimiting.Matchmaking)
            .WithName("AcceptOnlineMatch").Produces<MatchmakingResponse>().Produces(400).Produces(401).Produces(403).Produces(404).Produces(409).Produces(429);
        group.MapPost("/searches/{searchId:guid}/decline", DeclineAsync).AddEndpointFilter<SameOriginWriteFilter>()
            .RequireRateLimiting(LobbyRateLimiting.Matchmaking)
            .WithName("DeclineOnlineMatch").Produces<MatchmakingResponse>().Produces(400).Produces(401).Produces(403).Produces(404).Produces(409).Produces(429);
    }

    private static IResult EventsAsync(HttpContext context, IMatchmakingQueueStore queue, MatchmakingService matchmaking,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var session = PlayerAuthentication.Session(context);
        if (session is null) { return TypedResults.Unauthorized(); }
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Vary = "Cookie";
        context.Response.Headers["X-Accel-Buffering"] = "no";
        return TypedResults.ServerSentEvents(MatchmakingEventStream.ReadAsync(session, queue, matchmaking, clock, cancellationToken));
    }

    private static Task<IResult> JoinAsync(JoinMatchmakingRequest request, HttpContext context, MatchmakingService matchmaking, CancellationToken cancellationToken) =>
        ExecuteAsync(context, async session =>
        {
            return TypedResults.Ok(Map(await matchmaking.JoinAsync(session.PlayerId, session.Id, request.DisplayName, cancellationToken)));
        });

    private static Task<IResult> StatusAsync(HttpContext context, MatchmakingService matchmaking, CancellationToken cancellationToken) =>
        ExecuteAsync(context, async session =>
        {
            return TypedResults.Ok(Map(await matchmaking.GetAsync(session.PlayerId, session.Id, cancellationToken: cancellationToken)));
        });

    private static Task<IResult> SearchAsync(Guid searchId, HttpContext context, MatchmakingService matchmaking, CancellationToken cancellationToken) =>
        ExecuteAsync(context, async session =>
        {
            var search = await matchmaking.GetAsync(session.PlayerId, session.Id, searchId, cancellationToken);
            return search is null ? TypedResults.NotFound() : TypedResults.Ok(Map(search));
        });

    private static Task<IResult> CancelAsync(Guid searchId, HttpContext context, MatchmakingService matchmaking, CancellationToken cancellationToken) =>
        ExecuteAsync(context, async session =>
        {
            return await matchmaking.CancelAsync(session.PlayerId, session.Id, searchId, cancellationToken)
                ? TypedResults.NoContent() : TypedResults.NotFound();
        });

    private static Task<IResult> AcceptAsync(Guid searchId, MatchAcceptanceRequest _, HttpContext context, MatchmakingService matchmaking, CancellationToken cancellationToken) =>
        RespondAsync(searchId, true, context, matchmaking, cancellationToken);

    private static Task<IResult> DeclineAsync(Guid searchId, MatchAcceptanceRequest _, HttpContext context, MatchmakingService matchmaking, CancellationToken cancellationToken) =>
        RespondAsync(searchId, false, context, matchmaking, cancellationToken);

    private static Task<IResult> RespondAsync(Guid id, bool accept, HttpContext context, MatchmakingService matchmaking, CancellationToken cancellationToken) =>
        ExecuteAsync(context, async session =>
        {
            var search = await matchmaking.RespondAsync(session.PlayerId, session.Id, id, accept, cancellationToken);
            return search is null ? TypedResults.NotFound() : TypedResults.Ok(Map(search));
        });

    internal static MatchmakingResponse Map(MatchmakingSearch? search) => search is null
        ? new(null, ApiMatchmakingStatus.Idle, null, null, null, null, false)
        : new(search.Id, Enum.Parse<ApiMatchmakingStatus>(search.Status.ToString()), search.CreatedAt,
            search.Status == MatchmakingStatus.Ready ? null : search.ExpiresAt, search.MatchId, search.GameId, search.HasAccepted);

    private static async Task<IResult> ExecuteAsync(HttpContext context, Func<GuestSession, Task<IResult>> operation)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Vary = "Cookie";
        var session = PlayerAuthentication.Session(context);
        if (session is null)
        {
            return TypedResults.Unauthorized();
        }
        try { return await operation(session); }
        catch (UnauthorizedAccessException) { return TypedResults.Unauthorized(); }
        catch (MatchmakingConflictException exception) { return Problem(context, 409, exception.Message); }
        catch (ArgumentException) { return Problem(context, 400, "Use a nonblank display name of at most 32 characters without control characters."); }
    }

    private static IResult Problem(HttpContext context, int status, string title) =>
        TypedResults.Json(ApiProblems.Create(context, status, title), statusCode: status, contentType: "application/problem+json");
}
