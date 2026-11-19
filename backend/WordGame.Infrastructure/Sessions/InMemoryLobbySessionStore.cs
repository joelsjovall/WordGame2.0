using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Channels;
using WordGame.Application.Lobbies;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Infrastructure.Sessions;

public sealed class InMemoryLobbySessionStore(TimeProvider clock, LobbyRules rules, Func<LobbyCode>? generateCode = null) : ILobbySessionStore
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, Entry> lobbies = [];
    private readonly Dictionary<LobbyCode, Guid> codes = [];
    private readonly Dictionary<(Guid HostId, Guid RequestId), Guid> requests = [];
    private readonly Func<LobbyCode> nextCode = generateCode ?? GenerateCode;
    private readonly Dictionary<Guid, HashSet<Channel<Lobby?>>> watchers = [];

    public Task<LobbyCreation> CreateAsync(Player host, Guid requestId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A nonempty request ID is required.", nameof(requestId));
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = clock.GetUtcNow();
            RemoveExpired(now);
            var key = (host.Id, requestId);
            if (requests.TryGetValue(key, out var previousId))
            {
                var previous = lobbies[previousId].Lobby;
                if (!StringComparer.Ordinal.Equals(previous.Players[0].DisplayName, host.DisplayName))
                {
                    throw new LobbyCreationConflictException();
                }

                return Task.FromResult(new LobbyCreation(previous, false));
            }
            for (var attempt = 0; attempt < 32; attempt++)
            {
                var code = nextCode();
                if (codes.ContainsKey(code))
                {
                    continue;
                }

                var id = Guid.NewGuid();
                if (lobbies.ContainsKey(id))
                {
                    continue;
                }

                var lobby = new Lobby(id, code, host, now, now.Add(rules.Lifetime));
                cancellationToken.ThrowIfCancellationRequested();
                lobbies.Add(id, new Entry(lobby, key));
                codes.Add(code, id);
                requests.Add(key, id);
                return Task.FromResult(new LobbyCreation(lobby, true));
            }
            throw new InvalidOperationException("A unique lobby code could not be allocated.");
        }
    }

    public Task<Lobby?> GetAsync(Guid lobbyId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!lobbies.TryGetValue(lobbyId, out var entry))
            {
                return Task.FromResult<Lobby?>(null);
            }

            if (IsExpired(entry, clock.GetUtcNow()))
            {
                Remove(entry);
                return Task.FromResult<Lobby?>(null);
            }
            return Task.FromResult<Lobby?>(entry.Lobby);
        }
    }

    public Task<Lobby?> JoinAsync(LobbyCode code, Player player, Guid? expectedLobbyId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(player);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!codes.TryGetValue(code, out var id) || expectedLobbyId is { } expected && expected != id)
            {
                return Task.FromResult<Lobby?>(null);
            }

            var entry = lobbies[id];
            if (IsExpired(entry, clock.GetUtcNow()))
            {
                Remove(entry);
                return Task.FromResult<Lobby?>(null);
            }
            var joined = entry.Lobby.Join(player);
            cancellationToken.ThrowIfCancellationRequested();
            lobbies[id] = entry with { Lobby = joined };
            if (!ReferenceEquals(joined, entry.Lobby))
            {
                Publish(joined);
            }

            return Task.FromResult<Lobby?>(joined);
        }
    }

    public Task<Lobby?> ConfigureAsync(Guid lobbyId, Guid actor, long expectedVersion, LobbySettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = Find(lobbyId);
            if (entry is null)
            {
                return Task.FromResult<Lobby?>(null);
            }

            entry.Lobby.RequireHost(actor);
            RequireVersion(entry.Lobby, expectedVersion);
            var configured = entry.Lobby.Configure(actor, settings);
            lobbies[lobbyId] = entry with { Lobby = configured };
            Publish(configured);
            return Task.FromResult<Lobby?>(configured);
        }
    }

    // Reserve startup under the gate; engine work runs outside it. No other mutation can change the frozen roster/settings.
    public async Task<Lobby?> StartAsync(Guid lobbyId, Guid actor, long expectedVersion, Func<Lobby, Task<Guid>> createGame,
        CancellationToken cancellationToken = default)
    {
        Lobby starting;
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = Find(lobbyId);
            if (entry is null)
            {
                return null;
            }

            entry.Lobby.RequireHost(actor);
            if (entry.Lobby.Status == LobbyStatus.Started)
            {
                return entry.Lobby;
            }

            RequireVersion(entry.Lobby, expectedVersion);
            starting = entry.Lobby.BeginStart(actor);
            lobbies[lobbyId] = entry with { Lobby = starting };
            Publish(starting);
        }
        try
        {
            // Once reserved, finish even if the HTTP caller disconnects; retries retrieve the committed game ID.
            var gameId = await createGame(starting);
            lock (gate)
            {
                var complete = starting.CompleteStart(gameId);
                lobbies[lobbyId] = lobbies[lobbyId] with { Lobby = complete };
                Publish(complete);
                return complete;
            }
        }
        catch
        {
            lock (gate)
            {
                var restored = starting.AbortStart();
                lobbies[lobbyId] = lobbies[lobbyId] with { Lobby = restored };
                Publish(restored);
            }
            throw;
        }
    }

    public async IAsyncEnumerable<Lobby?> WatchAsync(Guid lobbyId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<Lobby?>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            AllowSynchronousContinuations = false
        });
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = Find(lobbyId);
            if (entry is null)
            {
                yield break;
            }

            if (!watchers.TryGetValue(lobbyId, out var subscribers))
            {
                watchers[lobbyId] = subscribers = [];
            }

            subscribers.Add(channel);
            channel.Writer.TryWrite(entry.Lobby);
        }
        try
        {
            await foreach (var state in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return state;
            }
        }
        finally
        {
            lock (gate)
            {
                if (watchers.TryGetValue(lobbyId, out var subscribers) && subscribers.Remove(channel) && subscribers.Count == 0)
                {
                    watchers.Remove(lobbyId);
                }
            }
        }
    }

    private Entry? Find(Guid id)
    {
        if (!lobbies.TryGetValue(id, out var entry))
        {
            return null;
        }

        if (!IsExpired(entry, clock.GetUtcNow()))
        {
            return entry;
        }

        Remove(entry);
        return null;
    }

    private static bool IsExpired(Entry entry, DateTimeOffset now) => entry.Lobby.Status != LobbyStatus.Starting && now >= entry.Lobby.ExpiresAt;
    private static void RequireVersion(Lobby lobby, long expectedVersion)
    {
        if (expectedVersion != lobby.Version)
        {
            throw new LobbyActionException("The lobby changed. Refresh and try again.");
        }
    }
    private void Publish(Lobby lobby)
    {
        if (watchers.TryGetValue(lobby.Id, out var subscribers))
        {
            foreach (var channel in subscribers)
            {
                channel.Writer.TryWrite(lobby);
            }
        }
    }

    public Task<int> CleanupAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(RemoveExpired(clock.GetUtcNow()));
        }
    }

    private int RemoveExpired(DateTimeOffset now)
    {
        var expired = lobbies.Values.Where(entry => IsExpired(entry, now)).ToArray();
        foreach (var entry in expired)
        {
            Remove(entry);
        }

        return expired.Length;
    }

    private void Remove(Entry entry)
    {
        lobbies.Remove(entry.Lobby.Id);
        codes.Remove(entry.Lobby.Code);
        requests.Remove(entry.CreationKey);
        if (watchers.Remove(entry.Lobby.Id, out var subscribers))
        {
            foreach (var channel in subscribers) { channel.Writer.TryWrite(null); channel.Writer.TryComplete(); }
        }
    }

    private static LobbyCode GenerateCode()
    {
        var characters = new char[LobbyCode.Length];
        for (var index = 0; index < characters.Length; index++)
        {
            characters[index] = LobbyCode.Alphabet[RandomNumberGenerator.GetInt32(LobbyCode.Alphabet.Length)];
        }

        return new LobbyCode(new string(characters));
    }

    private sealed record Entry(Lobby Lobby, (Guid HostId, Guid RequestId) CreationKey);
}
