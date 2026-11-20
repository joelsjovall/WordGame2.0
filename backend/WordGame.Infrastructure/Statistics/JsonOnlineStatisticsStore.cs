using System.Text.Json;
using WordGame.Application.Statistics;

namespace WordGame.Infrastructure.Statistics;

// Immutable per-match records avoid aggregate read/modify/write races. Suitable for the single-instance launch.
public sealed class JsonOnlineStatisticsStore(string directory) : IOnlineStatisticsStore
{
    private readonly string directory = Path.GetFullPath(directory);

    public async Task RecordAsync(OnlineMatchHistory match, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, $"{match.MatchId:D}.json");
        if (File.Exists(destination)) { return; }
        var temporary = Path.Combine(directory, $"{match.MatchId:D}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, match, cancellationToken: cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            try { File.Move(temporary, destination, overwrite: false); }
            catch (IOException) when (File.Exists(destination)) { /* A concurrent writer already recorded this match. */ }
        }
        finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
    }

    public async Task<IReadOnlyList<OnlineMatchHistory>> GetHistoryAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        var matches = new List<OnlineMatchHistory>();
        if (!Directory.Exists(directory)) { return matches; }
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var match = await JsonSerializer.DeserializeAsync<OnlineMatchHistory>(stream, cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("A stored Online match is empty.");
            if (match.Players.Any(player => player.PlayerId == playerId)) { matches.Add(match); }
        }
        return matches.OrderByDescending(match => match.CompletedAt).ThenBy(match => match.MatchId).ToArray();
    }
}
