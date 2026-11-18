using WordGame.Api.Errors;

namespace WordGame.Api.Authentication;

// JSON-only write endpoints also reject explicitly foreign browser origins.
internal sealed class SameOriginWriteFilter(IHostEnvironment environment, IConfiguration configuration) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        var origins = request.Headers.Origin;
        if (origins.Count == 0)
        {
            return await next(context);
        }

        var sameOrigin = $"{request.Scheme}://{request.Host}";
        var allowed = environment.IsDevelopment()
            ? configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [] : [];
        if (origins.Count != 1 || origins[0] is not { } origin ||
            (!StringComparer.OrdinalIgnoreCase.Equals(origin, sameOrigin) && !allowed.Contains(origin, StringComparer.OrdinalIgnoreCase)))
        {
            return Results.Json(ApiProblems.Create(context.HttpContext, 403, "This origin cannot create guest identities or lobbies."),
                statusCode: 403, contentType: "application/problem+json");
        }
        return await next(context);
    }
}
