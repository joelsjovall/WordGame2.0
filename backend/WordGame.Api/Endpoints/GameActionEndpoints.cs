using System.Globalization;
using WordGame.Api.Authentication;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Errors;
using WordGame.Application.Games;
using WordGame.Application.Synchronization;

namespace WordGame.Api.Endpoints;

internal static class GameActionEndpoints
{
    internal static void MapGameActionEndpoints(this WebApplication app) =>
        app.MapPost("/api/v1/games/{gameId:guid}/actions", ActAsync).RequireAuthorization()
            .RequireRateLimiting(LobbyRateLimiting.Gameplay)
            .AddEndpointFilter<SameOriginWriteFilter>().WithTags("Games").WithName("SubmitGameAction")
            .Produces<GameActionResponse>().Produces(400).Produces(401).Produces(403).Produces(404).Produces(409).Produces(429);

    private static async Task<IResult> ActAsync(Guid gameId, GameActionRequest request, HttpContext context,
        GameEventService events, GameEngine engine, CancellationToken cancellationToken)
    {
        var actor = LobbyRequests.Actor(context);
        if (actor == Guid.Empty)
        {
            return TypedResults.Unauthorized();
        }

        context.Response.Headers.CacheControl = "no-store";
        var state = await events.GetStateAsync(gameId, actor, cancellationToken);
        if (state is null)
        {
            return TypedResults.NotFound();
        }

        if (!long.TryParse(request.ExpectedVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var version) ||
            version < 0 || request.ExpectedVersion != version.ToString(CultureInfo.InvariantCulture) || request.ActionId == Guid.Empty)
        {
            return ApiProblems.Result(context, 400, "Provide a nonempty action ID and canonical nonnegative version.");
        }

        var action = new PlayerActionContext(request.ActionId, version);
        var result = request switch
        {
            SelectCategoryAction select => await engine.SelectCategoryAsync(gameId, actor, select.CategoryId, action, cancellationToken),
            PlaceBidAction bid => await engine.PlaceBidAsync(gameId, actor, bid.WordCount, action, cancellationToken, bid.Risk),
            SubmitSealedBidAction bid => await engine.SubmitSealedBidAsync(gameId, actor, bid.WordCount, action, cancellationToken, bid.Risk),
            CallBluffAction => await engine.CallBluffAsync(gameId, actor, action, cancellationToken),
            SubmitWordAction word => await engine.SubmitWordAsync(gameId, actor, word.Word, action, cancellationToken),
            LeaveMatchAction => await engine.LeaveAsync(gameId, actor, action, cancellationToken),
            ForfeitMatchAction => await engine.ForfeitAsync(gameId, actor, action, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported game action.")
        };
        if (result.Succeeded)
        {
            return TypedResults.Ok(new GameActionResponse(request.ActionId, result.State!.Version.ToString(CultureInfo.InvariantCulture)));
        }

        var status = result.Error!.Code switch
        {
            GameEngineErrorCode.InvalidAction => 400,
            GameEngineErrorCode.NotFound => 404,
            GameEngineErrorCode.PlayerNotInGame or GameEngineErrorCode.PlayerUnavailable => 403,
            _ => 409
        };
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("WordGame.Security")
            .LogWarning("Gameplay action rejected with code {Code}", result.Error.Code);
        return ApiProblems.Result(context, status, result.Error.Message);
    }
}
