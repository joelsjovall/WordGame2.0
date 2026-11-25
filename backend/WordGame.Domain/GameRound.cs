namespace WordGame.Domain;

public sealed class GameRound
{
    private readonly HashSet<Guid> playerIds;
    private readonly Dictionary<Guid, Bid> sealedBids = [];

    internal GameRound(Guid id, int number, IEnumerable<Guid> playerIds, Guid startingPlayerId, bool sealedOpening = false)
    {
        Id = DomainGuard.Id(id, nameof(id));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        Number = number;
        this.playerIds = playerIds.ToHashSet();
        RequirePlayer(startingPlayerId);
        StartingPlayerId = startingPlayerId;
        SealedOpening = sealedOpening;
        Turn = new PlayerTurn(startingPlayerId, 1);
    }

    public Guid Id { get; }
    public int Number { get; }
    public Guid StartingPlayerId { get; }
    public RoundStatus Status { get; private set; } = RoundStatus.CategorySelection;
    public PlayerTurn Turn { get; private set; }
    public Category? Category { get; private set; }
    public Bid? HighestBid { get; private set; }
    public Challenge? Challenge { get; private set; }
    public bool SealedOpening { get; }
    public bool SealedOpeningPending => SealedOpening && Status == RoundStatus.Bidding && HighestBid is null;
    public IReadOnlyList<Guid> SealedBidSubmitterIds => Array.AsReadOnly(sealedBids.Keys.ToArray());

    internal GameRound Copy()
    {
        var copy = new GameRound(Id, Number, playerIds, StartingPlayerId, SealedOpening)
        {
            Status = Status,
            Turn = Turn,
            Category = Category,
            HighestBid = HighestBid,
            Challenge = Challenge?.Copy()
        };
        foreach (var bid in sealedBids)
        {
            copy.sealedBids.Add(bid.Key, bid.Value);
        }
        return copy;
    }

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
        DomainGuard.State(!SealedOpeningPending, "Submit a sealed opening bid first.");
        DomainGuard.State(HighestBid is null || bid.WordCount > HighestBid.WordCount,
            "A bid must exceed the current highest bid.");
        HighestBid = bid;
    }

    public void SubmitSealedBid(Bid bid)
    {
        ArgumentNullException.ThrowIfNull(bid);
        RequirePlayer(bid.PlayerId);
        DomainGuard.State(SealedOpeningPending, "Sealed bids require the hidden opening phase.");
        DomainGuard.State(sealedBids.TryAdd(bid.PlayerId, bid), "This player has already submitted a sealed bid.");
    }

    public Bid RevealSealedBids(IReadOnlyList<Guid> rosterOrder)
    {
        ArgumentNullException.ThrowIfNull(rosterOrder);
        DomainGuard.State(SealedOpeningPending && sealedBids.Count == playerIds.Count &&
            rosterOrder.Count == playerIds.Count && rosterOrder.ToHashSet().SetEquals(playerIds),
            "Every roster member must submit one sealed bid before reveal.");
        var roster = rosterOrder.ToArray();
        var start = Array.IndexOf(roster, StartingPlayerId);
        var ordered = roster.Skip(start).Concat(roster.Take(start)).ToArray();
        var winner = ordered.Select(id => sealedBids[id]).OrderByDescending(bid => bid.WordCount).First();
        HighestBid = winner;
        var next = roster[(Array.IndexOf(roster, winner.PlayerId) + 1) % roster.Length];
        Turn = new PlayerTurn(next, checked(Turn.Number + 1));
        return winner;
    }

    // Selection of the next eligible player belongs to the Application turn policy.
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
