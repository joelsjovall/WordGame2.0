using WordGame.Application.Sessions;

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
    StaleState
}

public sealed record GameEngineError(GameEngineErrorCode Code, string Message);

// State is a detached backend snapshot, not a public transport response.
public sealed class GameEngineResult
{
    private GameEngineResult(SessionSnapshot? state, GameEngineError? error)
    {
        State = state;
        Error = error;
    }

    public bool Succeeded => Error is null;
    public SessionSnapshot? State { get; }
    public GameEngineError? Error { get; }

    internal static GameEngineResult Accepted(SessionSnapshot state) => new(state, null);
    internal static GameEngineResult Rejected(GameEngineErrorCode code, string message) => new(null, new(code, message));
}
