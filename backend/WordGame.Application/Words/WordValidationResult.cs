using WordGame.Domain;

namespace WordGame.Application.Words;

public sealed record WordValidationResult
{
    internal WordValidationResult(WordSubmissionStatus status, string? canonicalAnswerId)
    {
        Status = status;
        CanonicalAnswerId = canonicalAnswerId;
    }

    public WordSubmissionStatus Status { get; }
    public string? CanonicalAnswerId { get; }
}
