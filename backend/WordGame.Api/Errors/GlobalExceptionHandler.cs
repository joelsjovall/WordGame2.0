using Microsoft.AspNetCore.Diagnostics;

namespace WordGame.Api.Errors;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is BadHttpRequestException requestError)
        {
            await ApiProblems.WriteAsync(httpContext, ApiProblems.Create(httpContext, requestError.StatusCode,
                "Invalid request body. Use JSON matching the required request fields."), cancellationToken);
            return true;
        }
        logger.LogError(exception, "Unhandled request exception. Trace ID: {TraceId}", httpContext.TraceIdentifier);

        await ApiProblems.WriteAsync(httpContext,
            ApiProblems.Create(httpContext, 500, "An unexpected error occurred."), cancellationToken);
        return true;
    }
}
