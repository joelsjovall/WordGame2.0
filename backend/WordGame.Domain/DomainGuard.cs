namespace WordGame.Domain;

internal static class DomainGuard
{
    internal static Guid Id(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An ID must not be empty.", parameterName);
        }

        return value;
    }

    internal static string Text(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    internal static void State(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
