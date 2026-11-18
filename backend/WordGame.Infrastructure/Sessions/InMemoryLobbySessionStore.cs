using System.Security.Cryptography;
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

            if (clock.GetUtcNow() >= entry.Lobby.ExpiresAt)
            {
                Remove(entry);
                return Task.FromResult<Lobby?>(null);
            }
            return Task.FromResult<Lobby?>(entry.Lobby);
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
        var expired = lobbies.Values.Where(entry => now >= entry.Lobby.ExpiresAt).ToArray();
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
