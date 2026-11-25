using WordGame.Api.Errors;
using WordGame.Application.Categories;
using WordGame.Application.Challenges;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Sessions;

public sealed class GameplayTimeoutWorker : BackgroundService
{
    private readonly IGameSessionStore sessions;
    private readonly CategorySelectionService categories;
    private readonly ChallengeService challenges;
    private readonly GameEngine engine;
    private readonly SessionRetention retention;
    private readonly TimeProvider clock;
    private readonly ILogger<GameplayTimeoutWorker> logger;
    private readonly IHostEnvironment environment;
    private readonly TimeSpan interval;

    public GameplayTimeoutWorker(IGameSessionStore sessions, CategorySelectionService categories, ChallengeService challenges,
        TimeProvider clock, IConfiguration configuration, ILogger<GameplayTimeoutWorker> logger, IHostEnvironment environment,
        GameEngine engine, SessionRetention retention)
    {
        this.sessions = sessions;
        this.categories = categories;
        this.challenges = challenges;
        this.engine = engine;
        this.retention = retention;
        this.clock = clock;
        this.logger = logger;
        this.environment = environment;
        interval = configuration.GetValue<TimeSpan?>("Gameplay:TimeoutSweepInterval") ??
            configuration.GetValue("Gameplay:CategorySelectionSweepInterval", TimeSpan.FromSeconds(1));
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Gameplay timeout sweep interval must be positive.");
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(
        categories.Rules.Timeout is null ? Task.CompletedTask : RunAsync("Category selection",
            session => session.Game.CurrentRound?.Status == RoundStatus.CategorySelection &&
                session.CategorySelectionDeadline is { } deadline && clock.GetUtcNow() >= deadline,
            (session, token) => categories.ExpireAsync(session.Id, session.Version, token), stoppingToken),
        RunAsync("Challenge", session => session.Game.CurrentRound is
        { Status: RoundStatus.Challenge, Challenge: { Outcome: ChallengeOutcome.Pending } challenge } &&
                clock.GetUtcNow() >= challenge.Deadline,
            (session, token) => challenges.ResolveAsync(session.Id, session.Version, token), stoppingToken),
        RunAsync("Round progression", session => session.Game.CurrentRound?.Status == RoundStatus.Completed &&
            clock.GetUtcNow() >= session.UpdatedAt.AddSeconds(3),
            (session, token) => engine.NextRoundAsync(session.Id, session.Game.CurrentRound!.Id, session.Version, token), stoppingToken),
        RunAsync("Online presence", session => session.Game.Settings.Format == MatchFormat.Online &&
            session.Players.Any(player => !player.HasLeft && player.ConnectionCount == 0 && player.DisconnectedAt is { } at &&
                clock.GetUtcNow() - at >= retention.OnlineDisconnectedGrace),
            (session, token) => engine.ReconcileConnectionsAsync(session.Id, session.Version, token), stoppingToken));

    // Independent phase loops share the polling implementation; provider I/O cannot delay challenge expiry.
    private async Task RunAsync(string phase, Func<SessionSnapshot, bool> isDue,
        Func<SessionSnapshot, CancellationToken, Task<GameEngineResult>> resolve, CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval, clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    foreach (var session in await sessions.GetActiveAsync(stoppingToken))
                    {
                        if (!isDue(session))
                        {
                            continue;
                        }

                        try
                        {
                            // Stale revisions/finished rounds are harmless races and retry on a later sweep.
                            await resolve(session, stoppingToken);
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            SafeErrorLogging.Log(logger, environment, exception, $"{phase} timeout for game {session.Id}");
                        }
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    SafeErrorLogging.Log(logger, environment, exception, $"{phase} timeout sweep");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
