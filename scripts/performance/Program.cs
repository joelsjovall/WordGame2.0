using System.Diagnostics;
using WordGame.Application.Matchmaking;
using WordGame.Domain;
using WordGame.Infrastructure.Sessions;

var playerCount = args.Length > 0 ? int.Parse(args[0]) : 2_000;
var updates = args.Length > 1 ? int.Parse(args[1]) : 1_000;
var players = Enumerable.Range(0, playerCount).Select(_ => Guid.NewGuid()).ToArray();
var searches = players.Select(_ => Guid.NewGuid()).ToArray();
var now = DateTimeOffset.UtcNow;
using var store = new InMemoryMatchmakingQueueStore();

await store.ExecuteAsync(state =>
{
    for (var index = 0; index < playerCount; index++)
    {
        var player = new Player(players[index], $"Player {index}");
        var search = new MatchmakingSearch(searches[index], player, Guid.NewGuid(), MatchmakingStatus.Searching,
            now, now.AddMinutes(2), now);
        state.Searches.Add(search.Id, search);
        state.LatestByPlayer.Add(player.Id, search.Id);
        state.Waiting.AddLast(search.Id);
    }
    return Task.FromResult(0);
});

async Task UpdateAsync(int index) => await store.ExecuteAsync(state =>
{
    var search = state.Searches[searches[index]];
    state.Searches[search.Id] = search with { UpdatedAt = search.UpdatedAt.AddTicks(1) };
    return Task.FromResult(0);
});

for (var index = 0; index < 20; index++) await UpdateAsync(index % playerCount);
var samples = new double[3];
for (var sample = 0; sample < samples.Length; sample++)
{
    var clock = Stopwatch.StartNew();
    for (var index = 0; index < updates; index++) await UpdateAsync(index % playerCount);
    clock.Stop();
    samples[sample] = clock.Elapsed.TotalMilliseconds;
}
Array.Sort(samples);
Console.WriteLine($"{playerCount} queued players, {updates} updates: median {samples[1]:F1} ms ({samples[1] / updates:F3} ms/update)");
