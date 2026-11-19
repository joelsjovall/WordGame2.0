using WordGame.Application.Categories;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Lobbies;

public sealed class LobbyHostService(ILobbySessionStore lobbies, GameEngine engine, CategorySelectionService categories)
{
    public Task<Lobby?> ConfigureAsync(Guid id, Guid actor, long version, LobbySettings settings, CancellationToken cancellationToken = default) =>
        lobbies.ConfigureAsync(id, actor, version, settings, cancellationToken);

    public async Task<Lobby?> StartAsync(Guid id, Guid actor, long version, CancellationToken cancellationToken = default)
    {
        Guid? created = null;
        try
        {
            return await lobbies.StartAsync(id, actor, version, async frozen =>
            {
                created = await CreateGameAsync(frozen);
                return created.Value;
            }, cancellationToken);
        }
        catch
        {
            if (created is { } gameId)
            {
                await engine.CancelAsync(gameId);
            }

            throw;
        }
    }

    private async Task<Guid> CreateGameAsync(Lobby lobby)
    {
        var available = await categories.GetAvailableAsync();
        if (!available.Any(category => lobby.Settings.AllowedDifficulties.Contains(category.Difficulty)))
        {
            throw new LobbyActionException("No categories are available for the selected difficulties.");
        }

        var created = await engine.CreateAsync(lobby.Players, lobby.Settings.ForMatch(lobby.Players.Count));
        if (!created.Succeeded)
        {
            throw new LobbyActionException("The match could not be created.");
        }

        var id = created.State!.Id;
        try
        {
            if (!(await engine.InitializeAsync(id)).Succeeded || !(await engine.StartAsync(id)).Succeeded)
            {
                throw new LobbyActionException("The match could not start. Try again.");
            }

            return id;
        }
        catch
        {
            await engine.CancelAsync(id);
            throw;
        }
    }
}
