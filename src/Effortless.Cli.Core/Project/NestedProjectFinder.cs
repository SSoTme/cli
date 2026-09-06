namespace Effortless.Cli.Project;

/// <summary>
/// D12: finds nested effortless projects — subdirectories that carry their own
/// <c>effortless.json</c> (or legacy <c>ssotme.json</c>). Those are normally
/// excluded from a build, a clean or a describe; only the
/// <c>*WithSubprojects</c> scope walks into them.
/// </summary>
public static class NestedProjectFinder
{
    /// <summary>
    /// Returns the directory of every nested project under
    /// <paramref name="rootPath"/>, excluding the root project itself.
    /// </summary>
    public static IReadOnlyList<DirectoryInfo> Find(string rootPath)
    {
        var found = new List<DirectoryInfo>();
        if (string.IsNullOrWhiteSpace(rootPath)
            || !Directory.Exists(rootPath))
        {
            return found;
        }

        Walk(new DirectoryInfo(rootPath), found);
        return found;
    }

    private static void Walk(
        DirectoryInfo directory,
        List<DirectoryInfo> found)
    {
        foreach (var child in directory.GetDirectories())
        {
            if (child.IsIgnored())
            {
                continue;
            }

            if (HasProjectFile(child))
            {
                found.Add(child);
            }
        }

        foreach (var child in directory.GetDirectories())
        {
            if (child.IsIgnored())
            {
                continue;
            }

            Walk(child, found);
        }
    }

    private static bool HasProjectFile(DirectoryInfo directory) =>
        directory.GetFiles()
            .Any(file =>
                file.Name == "effortless.json"
                || file.Name == "ssotme.json");
}
