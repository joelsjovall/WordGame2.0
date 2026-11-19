using WordGame.Api.Authentication;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Errors;
using WordGame.Application.Matchmaking;
using WordGame.Application.Sessions;

namespace WordGame.Api.Endpoints;

internal static class MatchmakingEndpoints
{
    internal static void MapMatchmakingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/matchmaking").RequireAuthorization().WithTags("Matchmaking");
        group.MapPost("/searches", JoinAsync).AddEndpointFilter<SameOriginWriteFilter>()
            .RequireRateLimiting(LobbyRateLimiting.Matchmaking).WithName("JoinMatchmaking")
            .Produces<MatchmakingResponse>().Produces(400).Produces(401).Produces(403).Produces(409).Produces(429);
        group.MapGet("/status", StatusAsync).WithName("GetMatchmakingStatus").Produces<MatchmakingResponse>().Produces(401);
        group.MapGet("/searches/{searchId:guid}", SearchAsync).WithName("GetMatchmakingSearch")
            .Produces<MatchmakingResponse>().Produces(401).Produces(404);
        group.MapDelete("/searches/{searchId:guid}", CancelAsync).AddEndpointFilter<SameOriginWriteFilter>()
            .WithName("CancelMatchmaking").Produces(204).Produces(401).Produces(403).Produces(404).Produces(409);
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

    private static MatchmakingResponse Map(MatchmakingSearch? search) => search is null
        ? new(null, ApiMatchmakingStatus.Idle, null, null, null, null)
        : new(search.Id, Enum.Parse<ApiMatchmakingStatus>(search.Status.ToString()), search.CreatedAt, search.ExpiresAt, search.MatchId, null);

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
