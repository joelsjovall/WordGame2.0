using WordGame.Domain;

namespace WordGame.Application.Words;

public sealed record WordValidationResult
{
    internal WordValidationResult(string normalizedText, WordSubmissionStatus status, string? canonicalAnswerId)
    {
        NormalizedText = normalizedText;
        Status = status;
        CanonicalAnswerId = canonicalAnswerId;
    }

    public string NormalizedText { get; }
    public WordSubmissionStatus Status { get; }
    public string? CanonicalAnswerId { get; }
}
