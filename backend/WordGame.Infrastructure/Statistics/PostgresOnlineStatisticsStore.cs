using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using WordGame.Application.Statistics;

namespace WordGame.Infrastructure.Statistics;

// The match ID is the idempotency key; the database retains results across app restarts.
public sealed class PostgresOnlineStatisticsStore(NpgsqlDataSource dataSource) : IOnlineStatisticsStore
{
    private readonly SemaphoreSlim initialization = new(1, 1);
    private volatile bool initialized;

    public async Task RecordAsync(OnlineMatchHistory match, CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);
        if (match.Players.Count != 2) { throw new ArgumentException("Online history requires two players.", nameof(match)); }

        await using var command = dataSource.CreateCommand("""
            INSERT INTO online_match_history (match_id, completed_at, player_one, player_two, result)
            VALUES ($1, $2, $3, $4, $5) ON CONFLICT (match_id) DO NOTHING
            """);
        command.Parameters.AddWithValue(match.MatchId);
        command.Parameters.AddWithValue(match.CompletedAt);
        command.Parameters.AddWithValue(match.Players[0].PlayerId);
        command.Parameters.AddWithValue(match.Players[1].PlayerId);
        command.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(match));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OnlineMatchHistory>> GetHistoryAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("""
            SELECT result::text FROM online_match_history
            WHERE player_one = $1 OR player_two = $1
            ORDER BY completed_at DESC, match_id
            """);
        command.Parameters.AddWithValue(playerId);
        var matches = new List<OnlineMatchHistory>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            matches.Add(JsonSerializer.Deserialize<OnlineMatchHistory>(reader.GetString(0))
                ?? throw new InvalidDataException("A stored Online match is empty."));
        }
        return matches;
    }

    private async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        if (initialized) { return; }
        await initialization.WaitAsync(cancellationToken);
        try
        {
            if (initialized) { return; }
            await using var command = dataSource.CreateCommand("""
                CREATE TABLE IF NOT EXISTS online_match_history (
                    match_id uuid PRIMARY KEY,
                    completed_at timestamptz NOT NULL,
                    player_one uuid NOT NULL,
                    player_two uuid NOT NULL,
                    result jsonb NOT NULL
                )
                """);
            await command.ExecuteNonQueryAsync(cancellationToken);
            initialized = true;
        }
        finally { initialization.Release(); }
    }
}
