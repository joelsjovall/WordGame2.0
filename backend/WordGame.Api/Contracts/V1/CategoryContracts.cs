namespace WordGame.Api.Contracts.V1;

public enum ApiCategoryDifficulty { Easy, Medium, Hard }

public sealed record CategoryResponse(string Id, string Name, ApiCategoryDifficulty Difficulty);
public sealed record CategoryListResponse(IReadOnlyList<CategoryResponse> Items);
