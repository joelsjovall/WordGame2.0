using WordGame.Domain;

namespace WordGame.Application.Categories;

// Immutable answer data is retained on the server; this is not a public API contract.
public sealed record RoundCategorySelection(Guid RoundId, CategoryDefinition Definition, int ScoreMultiplier);
