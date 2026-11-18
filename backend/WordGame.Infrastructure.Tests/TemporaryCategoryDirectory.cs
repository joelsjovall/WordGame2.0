namespace WordGame.Infrastructure.Tests;

internal sealed class TemporaryCategoryDirectory : IDisposable
{
    private readonly string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wordgame-f007-tests"));

    internal TemporaryCategoryDirectory()
    {
        Root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    internal string Root { get; }

    internal string Write(string filename, string json)
    {
        var path = Path.Combine(Root, filename);
        File.WriteAllText(path, json);
        return path;
    }

    public void Dispose()
    {
        var target = Path.GetFullPath(Root);
        if (!target.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Test cleanup must stay inside its temporary parent directory.");
        }

        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }
    }
}
