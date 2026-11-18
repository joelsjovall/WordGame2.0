namespace WordGame.Domain;

public static class WordNormalizer
{
    // Retains V1's deterministic baseline. Alias matching belongs to the validation service.
    public static string Normalize(string? word) => (word ?? string.Empty).Trim().ToLowerInvariant();
}
