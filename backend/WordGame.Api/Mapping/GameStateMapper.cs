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
            snapshot.Game.CurrentRound is { } round ? MapRound(round) : null);
    }

    private static ApiSessionStatus MapStatus(SessionStatus status) => status switch
    {
        SessionStatus.Waiting => ApiSessionStatus.Waiting,
        SessionStatus.Starting => ApiSessionStatus.Starting,
        SessionStatus.Active => ApiSessionStatus.Active,
        SessionStatus.Completed => ApiSessionStatus.Completed,
        SessionStatus.Abandoned => ApiSessionStatus.Abandoned,
        _ => throw new InvalidOperationException("Unsupported session status.")
    };

    private static GameRoundResponse MapRound(Domain.GameRound round) => new(
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
        round.Challenge is { } challenge ? MapChallenge(challenge) : null);

    private static ChallengeResponse MapChallenge(Domain.Challenge challenge) => new(
        challenge.Id, challenge.Bid.PlayerId, challenge.ChallengerId, challenge.Bid.WordCount,
        challenge.StartedAt, challenge.Deadline, challenge.Outcome switch
        {
            Domain.ChallengeOutcome.Pending => ApiChallengeOutcome.Pending,
            Domain.ChallengeOutcome.Succeeded => ApiChallengeOutcome.Succeeded,
            Domain.ChallengeOutcome.Failed => ApiChallengeOutcome.Failed,
            Domain.ChallengeOutcome.Cancelled => ApiChallengeOutcome.Cancelled,
            _ => throw new InvalidOperationException("Unsupported challenge outcome.")
        }, challenge.AcceptedWordCount, challenge.Submissions.Select(word => new SubmittedWordResponse(word.Text,
            word.Status switch
            {
                Domain.WordSubmissionStatus.Accepted => ApiSubmissionStatus.Accepted,
                Domain.WordSubmissionStatus.Rejected => ApiSubmissionStatus.Rejected,
                Domain.WordSubmissionStatus.Duplicate => ApiSubmissionStatus.Duplicate,
                _ => throw new InvalidOperationException("Unsupported submission status.")
            })).ToArray());
}
