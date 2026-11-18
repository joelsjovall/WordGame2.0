using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using WordGame.Api.Contracts.V1;

namespace WordGame.Api.OpenApi;

internal sealed class ContractDocumentTransformer : IOpenApiDocumentTransformer
{
    private static readonly Type[] ContractTypes =
    [
        typeof(CreateGameRequest), typeof(JoinGameRequest), typeof(GameCreatedResponse), typeof(JoinGameResponse),
        typeof(GameStateResponse), typeof(GameActionRequest), typeof(GameActionResponse), typeof(GameEventResponse),
        typeof(ApiProblemDetails)
    ];

    public async Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "WordGame API";
        document.Info.Version = "v1";
        document.Info.Description = "Category metadata endpoints are implemented. Unmapped game schemas specify future authenticated handlers; they are not callable routes.";
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
            // Schemas requested outside endpoint metadata need explicit nullable
            // reference annotations for phase-dependent response fields.
            if (type == typeof(GameStateResponse) && schema is OpenApiSchema matchStateSchema &&
                matchStateSchema.Properties?.TryGetValue("result", out var matchResultSchema) == true &&
                matchResultSchema is OpenApiSchema matchResult)
            {
                matchResult.Type = (matchResult.Type ?? JsonSchemaType.Object) | JsonSchemaType.Null;
            }
            if (type == typeof(GameStateResponse) && schema is OpenApiSchema stateSchema &&
                stateSchema.Properties?.TryGetValue("round", out var roundSchema) == true &&
                roundSchema is OpenApiSchema round)
            {
                round.Type = (round.Type ?? JsonSchemaType.Object) | JsonSchemaType.Null;
                foreach (var name in new[] { "category", "highestBid", "challenge", "result" })
                {
                    if (round.Properties?.TryGetValue(name, out var field) == true && field is OpenApiSchema nullable)
                    {
                        nullable.Type = (nullable.Type ?? JsonSchemaType.Object) | JsonSchemaType.Null;
                    }
                }
            }
            document.AddComponent(type.Name, schema);
        }
    }
}
