using WordGame.Api.Contracts.V1;
using WordGame.Api.Errors;
using WordGame.Api.Mapping;
using WordGame.Application.Categories;
using WordGame.Domain;

namespace WordGame.Api.Endpoints;

internal static class CategoryEndpoints
{
    internal static void MapCategoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/categories").WithTags("Categories");
        group.MapGet("/", ListAsync).WithName("ListCategories")
            .WithSummary("List category metadata, optionally filtered by difficulty.")
            .Produces<CategoryListResponse>()
            .Produces<ApiProblemDetails>(400, "application/problem+json")
            .Produces<ApiProblemDetails>(500, "application/problem+json");
        group.MapGet("/{categoryId}", GetAsync).WithName("GetCategory")
            .WithSummary("Get category metadata without accepted answers or aliases.")
            .Produces<CategoryResponse>()
            .Produces<ApiProblemDetails>(404, "application/problem+json")
            .Produces<ApiProblemDetails>(500, "application/problem+json");
    }

    private static async Task<IResult> ListAsync(
        string? difficulty, HttpContext context, ICategoryProvider provider, CancellationToken cancellationToken)
    {
        CategoryDifficulty? selectedDifficulty = null;
        if (context.Request.Query.TryGetValue("difficulty", out var values))
        {
            selectedDifficulty = values.Count == 1 ? difficulty switch
            {
                "easy" => CategoryDifficulty.Easy,
                "medium" => CategoryDifficulty.Medium,
                "hard" => CategoryDifficulty.Hard,
                _ => null
            } : null;
            if (selectedDifficulty is null)
            {
                var problem = ApiProblems.Create(context, 400, "Invalid request.",
                    new Dictionary<string, string[]> { ["difficulty"] = ["Use easy, medium, or hard exactly once."] });
                return TypedResults.Json(problem, statusCode: 400, contentType: "application/problem+json");
            }
        }

        var categories = await provider.GetCategoriesAsync(selectedDifficulty, cancellationToken);
        return TypedResults.Ok(new CategoryListResponse(categories.Select(CategoryMapper.Map).ToArray()));
    }

    private static async Task<IResult> GetAsync(
        string categoryId, HttpContext context, ICategoryProvider provider, CancellationToken cancellationToken)
    {
        var category = await provider.GetByIdAsync(categoryId, cancellationToken);
        return category is not null
            ? TypedResults.Ok(CategoryMapper.Map(category.Category))
            : TypedResults.Json(ApiProblems.Create(context, 404, "Category not found."),
                statusCode: 404, contentType: "application/problem+json");
    }
}
