namespace WordGame.Domain;

public static class WordNormalizer
{
    // One shared rule for canonical values, aliases, and player submissions.
    public static string Normalize(string? word) =>
        string.Join(" ", (word ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
}
