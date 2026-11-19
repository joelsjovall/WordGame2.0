namespace WordGame.Domain;

public enum LobbyJoinFailure { Full, DisplayNameTaken, DifferentPlayerName }

public sealed class LobbyJoinException(LobbyJoinFailure reason) : InvalidOperationException("The lobby join request conflicts with current membership.")
{
    public LobbyJoinFailure Reason { get; } = reason;
}
