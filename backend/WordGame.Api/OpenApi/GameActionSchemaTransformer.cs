using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using WordGame.Api.Contracts.V1;

namespace WordGame.Api.OpenApi;

internal sealed class GameActionSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if (type != typeof(GameActionRequest) && typeof(GameActionRequest).IsAssignableFrom(type))
        {
            schema.Required ??= new HashSet<string>();
            schema.Required.Add("type");
        }
        return Task.CompletedTask;
    }
}
