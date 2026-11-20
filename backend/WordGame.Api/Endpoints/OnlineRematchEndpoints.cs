using WordGame.Api.Authentication;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Errors;
using WordGame.Application.Matchmaking;

namespace WordGame.Api.Endpoints;

internal static class OnlineRematchEndpoints
{
    internal static void MapOnlineRematchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/games/{gameId:guid}/rematch").RequireAuthorization().WithTags("Matchmaking")
            .RequireRateLimiting(LobbyRateLimiting.MatchmakingRead);
        group.MapGet("", GetAsync).WithName("GetOnlineRematch").Produces<OnlineRematchResponse>().Produces(401).Produces(404).Produces(409).Produces(429);
        group.MapPost("", RespondAsync).AddEndpointFilter<SameOriginWriteFilter>().WithName("RespondOnlineRematch")
            .RequireRateLimiting(LobbyRateLimiting.Matchmaking)
            .Produces<OnlineRematchResponse>().Produces(400).Produces(401).Produces(403).Produces(404).Produces(409).Produces(429);
    }
    private static Task<IResult> GetAsync(Guid gameId, HttpContext context, OnlineRematchService rematches, CancellationToken cancellationToken) =>
        Execute(context, (actor, credential) => rematches.GetAsync(gameId, actor, credential, cancellationToken));
    private static Task<IResult> RespondAsync(Guid gameId, OnlineRematchRequest request, HttpContext context, OnlineRematchService rematches, CancellationToken cancellationToken) =>
        Execute(context, (actor, credential) => rematches.RespondAsync(gameId, actor, credential,
            Enum.Parse<RematchAction>(request.Action.ToString()), cancellationToken));
    private static async Task<IResult> Execute(HttpContext context, Func<Guid, Guid, Task<OnlineRematch>> operation)
    {
        context.Response.Headers.CacheControl = "no-store";
        var guest = PlayerAuthentication.Session(context);
        if (guest is null) { return TypedResults.Unauthorized(); }
        try
        {
            var result = await operation(guest.PlayerId, guest.Id);
            return TypedResults.Ok(new OnlineRematchResponse(result.SourceGameId, Enum.Parse<ApiRematchStatus>(result.Status.ToString()),
                result.AcceptedPlayerIds, result.ExpiresAt, result.GameId));
        }
        catch (UnauthorizedAccessException) { return TypedResults.Unauthorized(); }
        catch (KeyNotFoundException) { return TypedResults.NotFound(); }
        catch (MatchmakingConflictException exception)
        { return TypedResults.Json(ApiProblems.Create(context, 409, exception.Message), statusCode: 409, contentType: "application/problem+json"); }
    }
}
