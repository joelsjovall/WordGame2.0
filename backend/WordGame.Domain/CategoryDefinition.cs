using System.Collections.Frozen;

namespace WordGame.Domain;

public sealed class CategoryDefinition
{
    private readonly FrozenDictionary<string, AcceptedAnswer> answersByNormalizedText;

    public CategoryDefinition(Category category, IEnumerable<AcceptedAnswer> answers)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(answers);
        var definitions = answers.ToArray();
        if (definitions.Length == 0 || definitions.Any(answer => answer is null))
        {
            throw new ArgumentException("A category needs at least one non-null accepted answer.", nameof(answers));
        }

        if (definitions.Select(answer => answer.Id).Distinct(StringComparer.Ordinal).Count() != definitions.Length)
        {
            throw new ArgumentException("Accepted answer IDs must be unique within a category.", nameof(answers));
        }

        var index = new Dictionary<string, AcceptedAnswer>(StringComparer.OrdinalIgnoreCase);
        foreach (var answer in definitions)
        {
            foreach (var text in answer.Aliases.Prepend(answer.Value))
            {
                var normalized = WordNormalizer.Normalize(text);
                if (index.TryGetValue(normalized, out var existing) && existing.Id != answer.Id)
                {
                    throw new ArgumentException(
                        $"Answer text '{normalized}' is ambiguous in category '{category.Id}'.", nameof(answers));
                }

                index[normalized] = answer;
            }
        }

        Category = category;
        Answers = Array.AsReadOnly(definitions);
        answersByNormalizedText = index.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public Category Category { get; }
    public IReadOnlyList<AcceptedAnswer> Answers { get; }

    public AcceptedAnswer? FindAnswer(string? text) =>
        answersByNormalizedText.GetValueOrDefault(WordNormalizer.Normalize(text));
}
