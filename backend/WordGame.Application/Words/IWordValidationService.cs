using WordGame.Domain;

namespace WordGame.Application.Words;

public interface IWordValidationService
{
    WordValidationResult Validate(
        CategoryDefinition category,
        string? word,
        IReadOnlyCollection<SubmittedWord> previousSubmissions);
}
