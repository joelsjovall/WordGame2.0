using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using WordGame.Api.Contracts.V1;

namespace WordGame.Api.OpenApi;

internal sealed class ContractDocumentTransformer : IOpenApiDocumentTransformer
{
    private static readonly Type[] ContractTypes =
    [
        typeof(CreateGameRequest), typeof(GameCreatedResponse),
        typeof(GameStateResponse), typeof(GameActionRequest), typeof(GameActionResponse), typeof(GameEventResponse),
        typeof(ApiProblemDetails)
    ];

    public async Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "WordGame API";
        document.Info.Version = "v1";
        document.Info.Description = "Category metadata, guest identity, private lobby creation, and authenticated game state/SSE endpoints are implemented. Game creation, lobby joining, and action schemas specify future handlers.";
        foreach (var type in ContractTypes)
        {
            var schema = await context.GetOrCreateSchemaAsync(type, null, cancellationToken);
            if (type == typeof(GameActionRequest) && schema is OpenApiSchema actionSchema)
            {
                // Unmapped contract variants are inline; avoid discriminator references to
                // components the endpoint generator has not registered. The literal type
                // properties still form a strongly typed discriminated union.
                actionSchema.Discriminator = null;
                foreach (var variant in actionSchema.AnyOf ?? [])
                {
                    if (variant is OpenApiSchema actionVariant)
                    {
                        actionVariant.Required ??= new HashSet<string>();
                        actionVariant.Required.Add("type");
                    }
                }
            }
            document.AddComponent(type.Name, schema);
        }
    }
}
