using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Application.Words;
using WordGame.Domain;

namespace WordGame.Application.Challenges;

public sealed class ChallengeService(IGameSessionStore sessions, SessionRetention retention,
    IWordValidationService validator, ChallengeRules rules, ScoringRules? scoring = null)
{
    private readonly GameCommandExecutor commands = new(sessions, retention);
    private readonly ScoringRules scoringRules = scoring ?? new ScoringRules();

    public Task<GameEngineResult> BeginAsync(Guid gameId, Guid playerId, PlayerActionContext context, CancellationToken cancellationToken = default) =>
        commands.PlayerUpdateAsync(gameId, playerId, context, session =>
        {
            var round = GamePhaseGuard.BiddingRound(session, playerId);
            GamePhaseGuard.Require(round.HighestBid is not null, "A bluff call requires an existing bid.");
            GamePhaseGuard.Require(session.CanTakeTurn(round.HighestBid!.PlayerId, retention.GraceFor(session.Game.Settings.Format)), "The bidder is unavailable.");
            var startedAt = session.UpdatedAt;
            round.BeginChallenge(new Challenge(Guid.NewGuid(), round.HighestBid, playerId, startedAt,
                startedAt.Add(session.Game.Settings.ChallengeDuration ?? rules.Duration)));
        }, cancellationToken);

    public async Task<GameEngineResult> SubmitAsync(Guid gameId, Guid playerId, string word,
        PlayerActionContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return GameEngineResult.Rejected(GameEngineErrorCode.InvalidAction, "A nonblank word is required.");
        }

        WordSubmissionStatus? status = null;
        var result = await commands.PlayerUpdateAsync(gameId, playerId, context, session =>
        {
            var round = GamePhaseGuard.ChallengeRound(session);
            var challenge = round.Challenge!;
            if (challenge.Bid.PlayerId != playerId || round.Turn.PlayerId != playerId)
            {
                throw new GameActionRejectedException(GameEngineErrorCode.NotYourTurn, "Only the challenged player can submit words.");
            }

            if (session.UpdatedAt >= challenge.Deadline)
            {
                throw new GameActionRejectedException(GameEngineErrorCode.ChallengeExpired, "The challenge deadline has passed.");
            }

            GamePhaseGuard.Require(session.SelectedCategory?.RoundId == round.Id, "The challenge requires its retained category snapshot.");
            var validation = validator.Validate(session.SelectedCategory!.Definition, word, challenge.Submissions);
            challenge.RecordSubmission(new SubmittedWord(playerId, word, session.UpdatedAt, validation.Status, validation.CanonicalAnswerId));
            status = validation.Status;
            if (challenge.HasMetBid)
            {
                Complete(session, round);
            }
        }, cancellationToken);
        return result.Succeeded ? GameEngineResult.Accepted(result.State!, status) : result;
    }

    public Task<GameEngineResult> ResolveAsync(Guid gameId, long? expectedVersion = null, CancellationToken cancellationToken = default) =>
        commands.UpdateAsync(gameId, session =>
        {
            var round = GamePhaseGuard.ChallengeRound(session);
            var challenge = round.Challenge!;
            GamePhaseGuard.Require(challenge.HasMetBid || session.UpdatedAt >= challenge.Deadline,
                "An incomplete challenge cannot end before its deadline.");
            session.ReconcileTurns(retention.GraceFor(session.Game.Settings.Format));
            if (session.Status == SessionStatus.Active)
            {
                Complete(session, round);
            }
        }, cancellationToken, expectedVersion);

    private void Complete(GameSession session, GameRound round)
    {
        GamePhaseGuard.Require(session.SelectedCategory?.RoundId == round.Id, "Scoring requires the retained category snapshot.");
        var challenge = round.Challenge!;
        challenge.Resolve(challenge.HasMetBid ? ChallengeOutcome.Succeeded : ChallengeOutcome.Failed);
        round.Complete();
        session.Game.RecordRoundScores(round.Id, scoringRules.Calculate(round, session.SelectedCategory!.ScoreMultiplier));
        if (session.Game.Rounds.Count == session.Game.Settings.RoundCount)
        {
            session.Game.Complete();
            session.Complete();
        }
    }
}
