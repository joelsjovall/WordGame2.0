using System.Text.Json;
using WordGame.Domain;

namespace WordGame.Infrastructure.Categories;

internal static class JsonCategoryFile
{
    internal static CategoryDefinition Load(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            RequireObject(root, "id", "name", "difficulty", "answers");
            var difficulty = ReadText(root, "difficulty") switch
            {
                "easy" => CategoryDifficulty.Easy,
                "medium" => CategoryDifficulty.Medium,
                "hard" => CategoryDifficulty.Hard,
                _ => throw new InvalidDataException("Property 'difficulty' must be easy, medium, or hard.")
            };
            var category = new Category(ReadText(root, "id"), ReadText(root, "name"), difficulty);
            var answers = ReadArray(root, "answers").Select(ReadAnswer).ToArray();
            return new CategoryDefinition(category, answers);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"Cannot load category file '{path}': {exception.Message}", exception);
        }
    }

    private static AcceptedAnswer ReadAnswer(JsonElement element)
    {
        RequireObject(element, "id", "value", "aliases");
        var aliases = ReadArray(element, "aliases").Select(alias => ReadTextValue(alias, "aliases")).ToArray();
        var id = element.TryGetProperty("id", out var identity) ? ReadTextValue(identity, "id") : null;
        return new AcceptedAnswer(ReadText(element, "value"), aliases, id);
    }

    private static void RequireObject(JsonElement element, params string[] allowedProperties)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Category and answer entries must be JSON objects.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new InvalidDataException($"Unknown property '{property.Name}'.");
            }

            if (!names.Add(property.Name))
            {
                throw new InvalidDataException($"Duplicate JSON property '{property.Name}'.");
            }
        }
    }

    private static string ReadText(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            throw new InvalidDataException($"Required property '{name}' is missing.");
        }

        return ReadTextValue(value, name);
    }

    private static string ReadTextValue(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw new InvalidDataException($"Property '{name}' must be a nonblank string.");
        }

        return element.GetString()!;
    }

    private static JsonElement.ArrayEnumerator ReadArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Required property '{name}' must be an array.");
        }

        return value.EnumerateArray();
    }
}
