using Microsoft.AspNetCore.Diagnostics;

namespace WordGame.Api.Errors;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment) : IExceptionHandler
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
        if (environment.IsProduction())
        {
            logger.LogError("Unhandled request exception of type {ExceptionType}. Trace ID: {TraceId}",
                exception.GetType().Name, httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogError(exception, "Unhandled request exception. Trace ID: {TraceId}", httpContext.TraceIdentifier);
        }

        await ApiProblems.WriteAsync(httpContext,
            ApiProblems.Create(httpContext, 500, "An unexpected error occurred."), cancellationToken);
        return true;
    }
}
