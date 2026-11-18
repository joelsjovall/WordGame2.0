using WordGame.Api.Contracts.V1;

namespace WordGame.Api.Errors;

internal static class ApiProblems
{
    internal static ApiProblemDetails Create(
        HttpContext context, int status, string title, IDictionary<string, string[]>? errors = null) => new()
        {
            Type = "about:blank",
            Status = status,
            Title = title,
            Instance = context.Request.Path,
            TraceId = context.TraceIdentifier,
            Errors = errors
        };

    internal static Task WriteAsync(
        HttpContext context, ApiProblemDetails problem, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return context.Response.WriteAsJsonAsync(problem, options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json", cancellationToken: cancellationToken);
    }
}
