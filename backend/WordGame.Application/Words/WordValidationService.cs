using WordGame.Domain;

namespace WordGame.Application.Words;

public sealed class WordValidationService : IWordValidationService
{
    public WordValidationResult Validate(
        CategoryDefinition category,
        string? word,
        IReadOnlyCollection<SubmittedWord> previousSubmissions)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(previousSubmissions);
        if (previousSubmissions.Any(submission => submission is null))
        {
            throw new ArgumentException("Submission history must not contain null entries.", nameof(previousSubmissions));
        }

        var normalized = WordNormalizer.Normalize(word);
        var answer = category.FindAnswer(normalized);
        if (answer is null)
        {
            return new WordValidationResult(normalized, WordSubmissionStatus.Rejected, null);
        }

        var duplicate = previousSubmissions.Any(submission =>
            submission.Status == WordSubmissionStatus.Accepted &&
            StringComparer.Ordinal.Equals(submission.CanonicalAnswerId, answer.Id));

        return new WordValidationResult(
            normalized,
            duplicate ? WordSubmissionStatus.Duplicate : WordSubmissionStatus.Accepted,
            answer.Id);
    }
}
