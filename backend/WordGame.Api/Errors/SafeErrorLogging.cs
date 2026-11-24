namespace WordGame.Api.Errors;

internal static class SafeErrorLogging
{
    internal static void Log(ILogger logger, IHostEnvironment environment, Exception exception, string operation)
    {
        if (environment.IsProduction())
        {
            logger.LogError("{Operation} failed with {ExceptionType}; a later sweep will retry.",
                operation, exception.GetType().Name);
            return;
        }

        logger.LogError(exception, "{Operation} failed; a later sweep will retry.", operation);
    }
}
