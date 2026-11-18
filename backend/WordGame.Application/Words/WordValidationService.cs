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

        var answer = category.FindAnswer(word);
        if (answer is null)
        {
            return new WordValidationResult(WordSubmissionStatus.Rejected, null);
        }

        var duplicate = previousSubmissions.Any(submission =>
            submission.Status == WordSubmissionStatus.Accepted &&
            StringComparer.Ordinal.Equals(submission.CanonicalAnswerId, answer.Id));

        return new WordValidationResult(
            duplicate ? WordSubmissionStatus.Duplicate : WordSubmissionStatus.Accepted,
            answer.Id);
    }
}
