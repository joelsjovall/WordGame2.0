using WordGame.Api.Contracts.V1;
using WordGame.Application.Statistics;

namespace WordGame.Api.Endpoints;

internal static class OnlineStatisticsEndpoints
{
    internal static void MapOnlineStatisticsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/players/me/statistics", StatisticsAsync).RequireAuthorization()
            .WithTags("Players").WithName("GetOnlinePlayerStatistics").Produces<OnlineStatisticsResponse>().Produces(401);
        app.MapGet("/api/v1/players/me/history", HistoryAsync).RequireAuthorization()
            .WithTags("Players").WithName("GetOnlinePlayerHistory").Produces<OnlineHistoryResponse>().Produces(401);
    }

    private static async Task<IResult> StatisticsAsync(HttpContext context, IOnlineStatisticsStore store, CancellationToken cancellationToken)
    {
        var actor = LobbyRequests.Actor(context);
        if (actor == Guid.Empty) { return TypedResults.Unauthorized(); }
        context.Response.Headers.CacheControl = "no-store";
        var statistics = OnlinePlayerStatistics.Calculate(actor, await store.GetHistoryAsync(actor, cancellationToken));
        return TypedResults.Ok(new OnlineStatisticsResponse(statistics.MatchesPlayed, statistics.Wins, statistics.Losses,
            statistics.Draws, statistics.WinPercentage, statistics.SuccessfulBluffChallenges, statistics.FailedBluffChallenges));
    }

    private static async Task<IResult> HistoryAsync(HttpContext context, IOnlineStatisticsStore store, CancellationToken cancellationToken)
    {
        var actor = LobbyRequests.Actor(context);
        if (actor == Guid.Empty) { return TypedResults.Unauthorized(); }
        context.Response.Headers.CacheControl = "no-store";
        var matches = await store.GetHistoryAsync(actor, cancellationToken);
        return TypedResults.Ok(new OnlineHistoryResponse(matches.Take(50).Select(match => new OnlineHistoryMatchResponse(
            match.MatchId, match.CompletedAt, Enum.Parse<ApiMatchEndReason>(match.Reason.ToString()), match.Players.Select(player =>
                new OnlineHistoryPlayerResponse(player.PlayerId, player.DisplayName, player.Score, player.Won, player.Draw,
                    player.SuccessfulBluffChallenges, player.FailedBluffChallenges)).ToArray())).ToArray()));
    }
}
