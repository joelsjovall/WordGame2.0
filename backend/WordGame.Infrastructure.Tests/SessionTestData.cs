using WordGame.Application.Categories;
using WordGame.Application.Challenges;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Application.Words;
using WordGame.Domain;
using WordGame.Infrastructure.Sessions;

namespace WordGame.Infrastructure.Tests;

internal static class SessionTestData
{
    internal static GameEngine Engine(IGameSessionStore store, IWordValidationService? validator = null)
    {
        var retention = new SessionRetention(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(5));
        return new GameEngine(store, retention, new CategorySelectionService(new EngineCategoryProvider(), new CategorySelectionRules(null), store, retention),
            Challenges(store, retention, validator: validator));
    }

    internal static ChallengeService Challenges(IGameSessionStore store, SessionRetention retention, ChallengeRules? rules = null,
        IWordValidationService? validator = null) =>
        new(store, retention, validator ?? new WordValidationService(), rules ?? new ChallengeRules(TimeSpan.FromSeconds(30)));

    private sealed class EngineCategoryProvider : ICategoryProvider
    {
        private static readonly CategoryDefinition Cars = new(new Category("cars", "Cars", CategoryDifficulty.Easy), [new AcceptedAnswer("Volvo")]);
        public Task<CategoryDefinition?> GetByIdAsync(string categoryId, CancellationToken cancellationToken = default) =>
            Task.FromResult(categoryId == Cars.Category.Id ? Cars : null);
        public Task<IReadOnlyList<Category>> GetCategoriesAsync(CategoryDifficulty? difficulty = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Category>>(difficulty is null or CategoryDifficulty.Easy ? [Cars.Category] : []);
    }

    internal static Player[] Players(int count = 2) => Enumerable.Range(1, count)
        .Select(index => new Player(Guid.NewGuid(), $"Player {index}")).ToArray();

    internal static InMemoryGameSessionStore Store(ManualSessionClock clock, TimeSpan? retention = null) =>
        new(clock, new SessionRetention(TimeSpan.FromMinutes(2),
            retention ?? TimeSpan.FromMinutes(10), retention ?? TimeSpan.FromMinutes(5)));

    internal static void Activate(GameSession session)
    {
        session.BeginStarting();
        session.Game.StartRound(Guid.NewGuid(), session.Game.Players[0].Id);
        session.Activate();
    }

    internal static void Complete(GameSession session)
    {
        Activate(session);
        var round = session.Game.CurrentRound!;
        var bidder = session.Game.Players[0].Id;
        var caller = session.Game.Players[1].Id;
        round.SelectCategory(bidder, new Category("cars", "Cars", CategoryDifficulty.Easy));
        round.RecordBid(new Bid(bidder, 1));
        round.AssignTurn(caller);
        var challenge = new Challenge(Guid.NewGuid(), round.HighestBid!, caller,
            session.UpdatedAt, session.UpdatedAt.AddSeconds(30));
        round.BeginChallenge(challenge);
        challenge.Resolve(ChallengeOutcome.Failed);
        round.Complete();
        session.Game.Complete();
        session.Complete();
    }
}

internal sealed class ManualSessionClock : TimeProvider
{
    private DateTimeOffset now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => now;
    internal void Advance(TimeSpan duration) => now += duration;
}
