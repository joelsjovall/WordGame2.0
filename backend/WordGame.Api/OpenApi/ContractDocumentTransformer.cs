using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using WordGame.Api.Contracts.V1;

namespace WordGame.Api.OpenApi;

internal sealed class ContractDocumentTransformer : IOpenApiDocumentTransformer
{
    private static readonly Type[] ContractTypes =
    [
        typeof(GameStateResponse), typeof(GameActionRequest), typeof(GameActionResponse), typeof(GameEventResponse),
        typeof(ApiProblemDetails)
    ];

    public async Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "WordGame API";
        document.Info.Version = "v1";
        document.Info.Description = "Category metadata, guest identity, private lobby lifecycle, and authenticated gameplay/state/SSE endpoints are implemented.";
        foreach (var type in ContractTypes)
        {
            var schema = await context.GetOrCreateSchemaAsync(type, null, cancellationToken);
            document.AddComponent(type.Name, schema);
        }
    }
}
