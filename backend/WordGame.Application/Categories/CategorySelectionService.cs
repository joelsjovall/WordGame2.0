using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Categories;

// Provider I/O happens before the engine's short atomic update; application/phase guards run again inside it.
public sealed class CategorySelectionService(ICategoryProvider provider, CategorySelectionRules rules,
    IGameSessionStore sessions, SessionRetention retention)
{
    private readonly GameCommandExecutor commands = new(sessions, retention);
    public CategorySelectionRules Rules => rules;

    public async Task<GameEngineResult> SelectAsync(Guid gameId, Guid playerId, string categoryId,
        PlayerActionContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(categoryId) || categoryId.Length > 128)
        {
            return GameEngineResult.Rejected(GameEngineErrorCode.InvalidAction, "A category ID of at most 128 characters is required.");
        }

        var definition = await ResolveAsync(categoryId.Trim(), cancellationToken);
        if (definition is null)
        {
            return GameEngineResult.Rejected(GameEngineErrorCode.CategoryUnavailable, "The category is unavailable.");
        }

        return await commands.PlayerUpdateAsync(gameId, playerId, context, session =>
        {
            RequireSelectionPhase(session);
            if (session.Game.CurrentRound!.Turn.PlayerId != playerId)
            {
                throw new GameActionRejectedException(GameEngineErrorCode.NotYourTurn, "The player does not hold the current turn.");
            }

            Apply(session, playerId, definition);
        }, cancellationToken);
    }

    public async Task<GameEngineResult> ExpireAsync(Guid gameId, long expectedVersion, CancellationToken cancellationToken = default)
    {
        var snapshot = await sessions.GetAsync(gameId, cancellationToken);
        var definition = rules.TimeoutBehavior == CategoryTimeoutBehavior.AutoSelect
            ? await ResolveTimeoutAsync(snapshot?.Game.Settings.AllowedDifficulties,
                snapshot?.Game.Settings.AllowedCategoryIds, cancellationToken) : null;
        return await commands.UpdateAsync(gameId, session =>
        {
            RequireSelectionPhase(session);
            if (session.CategorySelectionDeadline is not { } deadline || session.UpdatedAt < deadline)
            {
                throw new InvalidOperationException("Category selection has not expired.");
            }

            session.ReconcileTurns(retention.GraceFor(session.Game.Settings.Format));
            if (session.Status != SessionStatus.Active)
            {
                return;
            }

            if (definition is null)
            {
                session.Abandon();
            }
            else
            {
                Apply(session, session.Game.CurrentRound!.Turn.PlayerId, definition, timeout: true);
            }
        }, cancellationToken, expectedVersion);
    }

    public async Task<IReadOnlyList<Category>> GetAvailableAsync(CategoryDifficulty? difficulty = null, CancellationToken cancellationToken = default)
    {
        if (difficulty.HasValue && !Enum.IsDefined(difficulty.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(difficulty));
        }

        var catalog = await provider.GetCategoriesAsync(difficulty, cancellationToken);
        return Array.AsReadOnly(catalog.Where(category => rules.AllowedDifficulties.Contains(category.Difficulty) &&
            (!difficulty.HasValue || difficulty == category.Difficulty)).OrderBy(category => category.Id, StringComparer.Ordinal).ToArray());
    }

    private async Task<CategoryDefinition?> ResolveAsync(string categoryId, CancellationToken cancellationToken = default)
    {
        var catalog = await GetAvailableAsync(cancellationToken: cancellationToken);
        if (!catalog.Any(category => StringComparer.Ordinal.Equals(category.Id, categoryId)))
        {
            return null;
        }

        return await LoadAsync(categoryId, cancellationToken);
    }

    private async Task<CategoryDefinition?> ResolveTimeoutAsync(IReadOnlyList<CategoryDifficulty>? allowed,
        IReadOnlyList<string>? allowedIds, CancellationToken cancellationToken = default)
    {
        foreach (var category in await GetAvailableAsync(cancellationToken: cancellationToken))
        {
            if (allowed is not null && !allowed.Contains(category.Difficulty) ||
                allowedIds is not null && !allowedIds.Contains(category.Id))
            {
                continue;
            }

            var definition = await LoadAsync(category.Id, cancellationToken);
            if (definition is not null)
            {
                return definition;
            }
        }
        return null;
    }

    private void Apply(GameSession session, Guid playerId, CategoryDefinition definition, bool timeout = false)
    {
        if (session.CategorySelectionDeadline is { } deadline && (timeout ? session.UpdatedAt < deadline : session.UpdatedAt >= deadline))
        {
            throw new InvalidOperationException(timeout ? "Category selection has not expired." : "Category selection has expired.");
        }

        if (!rules.AllowedDifficulties.Contains(definition.Category.Difficulty) ||
            session.Game.Settings.AllowedDifficulties is { } allowed && !allowed.Contains(definition.Category.Difficulty) ||
            session.Game.Settings.AllowedCategoryIds is { } ids && !ids.Contains(definition.Category.Id))
        {
            throw new InvalidOperationException("This difficulty is unavailable.");
        }

        session.SelectCategory(playerId, definition, rules.ScoreMultipliers[definition.Category.Difficulty]);
    }

    private async Task<CategoryDefinition?> LoadAsync(string categoryId, CancellationToken cancellationToken)
    {
        var definition = await provider.GetByIdAsync(categoryId, cancellationToken);
        if (definition is null)
        {
            return null;
        }

        if (!StringComparer.Ordinal.Equals(definition.Category.Id, categoryId) || definition.Answers.Count == 0)
        {
            throw new InvalidDataException("The category provider returned an inconsistent definition.");
        }

        return rules.AllowedDifficulties.Contains(definition.Category.Difficulty) ? definition : null;
    }

    private static void RequireSelectionPhase(GameSession session)
    {
        if (session.Status != SessionStatus.Active || session.Game.CurrentRound?.Status != RoundStatus.CategorySelection)
        {
            throw new InvalidOperationException("Category selection requires an active selection round.");
        }
    }
}
