using System.Globalization;
using WordGame.Api.Contracts.V1;
using WordGame.Application.Sessions;
using Domain = WordGame.Domain;

namespace WordGame.Api.Mapping;

public static class GameStateMapper
{
    public static GameStateResponse Map(SessionSnapshot snapshot, DateTimeOffset serverTime)
    {
        var connected = snapshot.Players.ToDictionary(player => player.PlayerId, player => player.ConnectionCount > 0);
        return new GameStateResponse(snapshot.Id, MapStatus(snapshot.Status), snapshot.Version.ToString(CultureInfo.InvariantCulture), serverTime,
            snapshot.Game.Players.Select(player => new GamePlayerResponse(
                player.Id, player.DisplayName, snapshot.Game.Scores[player.Id].Points, connected[player.Id])).ToArray(),
            snapshot.Game.CurrentRound is { } round ? MapRound(round, snapshot.Game.RoundResults.GetValueOrDefault(round.Id), snapshot.CategorySelectionDeadline) : null,
            new MatchSettingsResponse(MapFormat(snapshot.Game.Settings.Format), snapshot.Game.Settings.RoundCount,
                snapshot.Game.Settings.ChallengeDuration?.TotalSeconds,
                snapshot.Game.Settings.AllowedDifficulties?.Select(value => Enum.Parse<ApiCategoryDifficulty>(value.ToString())).ToArray()),
            snapshot.Game.Result is { } result ? MapMatchResult(result) : null);
    }

    private static ApiMatchFormat MapFormat(Domain.MatchFormat format) => format switch
    {
        Domain.MatchFormat.Party => ApiMatchFormat.Party,
        Domain.MatchFormat.Online => ApiMatchFormat.Online,
        _ => throw new InvalidOperationException("Unsupported match format.")
    };

    private static MatchResultResponse MapMatchResult(Domain.MatchResult result) => new(
        result.GameId, MapFormat(result.Format), result.CompletedRoundCount, result.IsTie, result.WinnerIds,
        result.Standings.Select(standing => new MatchStandingResponse(standing.PlayerId, standing.DisplayName, standing.Score, standing.Rank)).ToArray());

    internal static ApiSessionStatus MapStatus(SessionStatus status) => status switch
    {
        SessionStatus.Waiting => ApiSessionStatus.Waiting,
        SessionStatus.Starting => ApiSessionStatus.Starting,
        SessionStatus.Active => ApiSessionStatus.Active,
        SessionStatus.Completed => ApiSessionStatus.Completed,
        SessionStatus.Abandoned => ApiSessionStatus.Abandoned,
        _ => throw new InvalidOperationException("Unsupported session status.")
    };

    public static RoundResultResponse MapResult(Domain.RoundResult result) => new(
        result.RoundId, result.RoundNumber, CategoryMapper.Map(result.Category), result.ChallengeId,
        result.BidderId, result.CallerId, result.WinnerId, result.RequiredWordCount, result.AcceptedWordCount,
        result.Outcome == Domain.ChallengeOutcome.Succeeded ? ApiChallengeOutcome.Succeeded : ApiChallengeOutcome.Failed,
        result.Submissions.Select(MapSubmission).ToArray(),
        result.Scores.Select(score => new RoundPlayerScoreResponse(score.PlayerId, score.PointsAwarded, score.TotalPoints)).ToArray());

    private static GameRoundResponse MapRound(Domain.GameRound round, Domain.RoundResult? result, DateTimeOffset? selectionDeadline) => new(
        round.Id, round.Number, round.Status switch
        {
            Domain.RoundStatus.CategorySelection => ApiRoundStatus.CategorySelection,
            Domain.RoundStatus.Bidding => ApiRoundStatus.Bidding,
            Domain.RoundStatus.Challenge => ApiRoundStatus.Challenge,
            Domain.RoundStatus.Completed => ApiRoundStatus.Completed,
            Domain.RoundStatus.Cancelled => ApiRoundStatus.Cancelled,
            _ => throw new InvalidOperationException("Unsupported round status.")
        }, round.Turn.PlayerId, round.Category is { } category ? CategoryMapper.Map(category) : null,
        round.HighestBid is { } bid ? new BidResponse(bid.PlayerId, bid.WordCount) : null,
        round.Challenge is { } challenge ? MapChallenge(challenge) : null, result is null ? null : MapResult(result), selectionDeadline);

    private static ChallengeResponse MapChallenge(Domain.Challenge challenge) => new(
        challenge.Id, challenge.Bid.PlayerId, challenge.ChallengerId, challenge.Bid.WordCount,
        challenge.StartedAt, challenge.Deadline, challenge.Outcome switch
        {
            Domain.ChallengeOutcome.Pending => ApiChallengeOutcome.Pending,
            Domain.ChallengeOutcome.Succeeded => ApiChallengeOutcome.Succeeded,
            Domain.ChallengeOutcome.Failed => ApiChallengeOutcome.Failed,
            Domain.ChallengeOutcome.Cancelled => ApiChallengeOutcome.Cancelled,
            _ => throw new InvalidOperationException("Unsupported challenge outcome.")
        }, challenge.AcceptedWordCount, challenge.Submissions.Select(MapSubmission).ToArray());

    private static SubmittedWordResponse MapSubmission(Domain.SubmittedWord word) => new(word.Text,
        word.Status switch
        {
            Domain.WordSubmissionStatus.Accepted => ApiSubmissionStatus.Accepted,
            Domain.WordSubmissionStatus.Rejected => ApiSubmissionStatus.Rejected,
            Domain.WordSubmissionStatus.Duplicate => ApiSubmissionStatus.Duplicate,
            _ => throw new InvalidOperationException("Unsupported submission status.")
        });
}
