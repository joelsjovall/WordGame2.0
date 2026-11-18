using WordGame.Application.Challenges;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Games;

public enum GameEngineErrorCode
{
    NotFound,
    InvalidAction,
    InvalidState,
    PlayerNotInGame,
    NotYourTurn,
    PlayerUnavailable,
    DuplicateAction,
    StaleState,
    CategoryUnavailable,
    ChallengeExpired
}

public sealed record GameEngineError(GameEngineErrorCode Code, string Message);

// State is a detached backend snapshot, not a public transport response.
public sealed class GameEngineResult
{
    private GameEngineResult(SessionSnapshot? state, GameEngineError? error, WordSubmissionStatus? submissionStatus = null)
    {
        State = state;
        Error = error;
        SubmissionStatus = submissionStatus;
        ChallengeResult = state?.Game.CurrentRound is { Challenge: { Outcome: not ChallengeOutcome.Pending } } round
            ? Challenges.ChallengeResult.From(round) : null;
    }

    public bool Succeeded => Error is null;
    public SessionSnapshot? State { get; }
    public GameEngineError? Error { get; }
    public WordSubmissionStatus? SubmissionStatus { get; }
    public ChallengeResult? ChallengeResult { get; }

    internal static GameEngineResult Accepted(SessionSnapshot state, WordSubmissionStatus? submissionStatus = null) => new(state, null, submissionStatus);
    internal static GameEngineResult Missing() => Rejected(GameEngineErrorCode.NotFound, "The game was not found.");
    internal static GameEngineResult Rejected(GameEngineErrorCode code, string message) => new(null, new(code, message));
}
