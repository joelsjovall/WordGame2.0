namespace WordGame.Domain;

public sealed class GameRound
{
    private readonly HashSet<Guid> playerIds;

    internal GameRound(Guid id, int number, IEnumerable<Guid> playerIds, Guid startingPlayerId)
    {
        Id = DomainGuard.Id(id, nameof(id));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        Number = number;
        this.playerIds = playerIds.ToHashSet();
        RequirePlayer(startingPlayerId);
        Turn = new PlayerTurn(startingPlayerId, 1);
    }

    public Guid Id { get; }
    public int Number { get; }
    public RoundStatus Status { get; private set; } = RoundStatus.CategorySelection;
    public PlayerTurn Turn { get; private set; }
    public Category? Category { get; private set; }
    public Bid? HighestBid { get; private set; }
    public Challenge? Challenge { get; private set; }

    internal GameRound Copy() => new(Id, Number, playerIds, Turn.PlayerId)
    {
        Status = Status,
        Turn = Turn,
        Category = Category,
        HighestBid = HighestBid,
        Challenge = Challenge?.Copy()
    };

    public void SelectCategory(Guid playerId, Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        RequireTurn(playerId);
        DomainGuard.State(Status == RoundStatus.CategorySelection, "Category selection has ended.");
        Category = category;
        Status = RoundStatus.Bidding;
    }

    public void RecordBid(Bid bid)
    {
        ArgumentNullException.ThrowIfNull(bid);
        RequireTurn(bid.PlayerId);
        DomainGuard.State(Status == RoundStatus.Bidding, "Bids require the bidding phase.");
        DomainGuard.State(HighestBid is null || bid.WordCount > HighestBid.WordCount,
            "A bid must exceed the current highest bid.");
        HighestBid = bid;
    }

    // Selection of the next player belongs to the future turn-management policy.
    public void AssignTurn(Guid playerId)
    {
        RequirePlayer(playerId);
        DomainGuard.State(Status is RoundStatus.CategorySelection or RoundStatus.Bidding,
            "Turns can only be assigned during category selection or bidding.");
        DomainGuard.State(playerId != Turn.PlayerId, "The next player must differ from the active player.");
        Turn = new PlayerTurn(playerId, checked(Turn.Number + 1));
    }

    public void BeginChallenge(Challenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        DomainGuard.State(Status == RoundStatus.Bidding, "Challenges require the bidding phase.");
        DomainGuard.State(HighestBid is not null && HighestBid == challenge.Bid,
            "The challenge must refer to the highest bid.");
        RequireTurn(challenge.ChallengerId);
        DomainGuard.State(challenge.Outcome == ChallengeOutcome.Pending && challenge.Submissions.Count == 0,
            "The challenge must be new and unresolved.");
        var challengeTurn = new PlayerTurn(challenge.Bid.PlayerId, checked(Turn.Number + 1));
        challenge.AttachToRound();
        Challenge = challenge;
        Turn = challengeTurn;
        Status = RoundStatus.Challenge;
    }

    public void Complete()
    {
        DomainGuard.State(Status == RoundStatus.Challenge && Challenge?.Outcome is
            ChallengeOutcome.Succeeded or ChallengeOutcome.Failed,
            "A round requires a resolved challenge before completion.");
        Status = RoundStatus.Completed;
    }

    internal void Cancel()
    {
        DomainGuard.State(Status is not (RoundStatus.Completed or RoundStatus.Cancelled),
            "The round has already ended.");
        Challenge?.Cancel();
        Status = RoundStatus.Cancelled;
    }

    private void RequirePlayer(Guid playerId) =>
        DomainGuard.State(playerIds.Contains(playerId), "The player must belong to this game.");

    private void RequireTurn(Guid playerId)
    {
        RequirePlayer(playerId);
        DomainGuard.State(Turn.PlayerId == playerId, "The player does not hold the current turn.");
    }
}
