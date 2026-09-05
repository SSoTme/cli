#nullable enable
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli.LocalTools;

/// <summary>
/// Discovers the local tools of one project: every folder directly under
/// <c>&lt;root&gt;/effortless-tools/</c> that is a valid tool (step 12).
/// A parent project's tools are never inherited; each project has its own
/// folder, exactly like project settings.
/// </summary>
public sealed class LocalToolCatalog
{
    public const string ToolsDirectoryName = "effortless-tools";
    public const string ManifestFileName = "tool.json";
    public const string LedgerKeyPrefix = "local-";

    private static readonly Regex ValidName =
        new("^[a-z0-9][a-z0-9-]*$", RegexOptions.Compiled);

    private readonly Dictionary<string, LocalTool> _tools;

    private LocalToolCatalog(
        string projectRoot,
        string toolsDirectory,
        List<LocalTool> tools,
        List<LocalToolProblem> problems)
    {
        ProjectRoot = projectRoot;
        ToolsDirectory = toolsDirectory;
        Tools = tools;
        Problems = problems;
        _tools = tools.ToDictionary(
            tool => tool.Name,
            StringComparer.Ordinal);
    }

    public string ProjectRoot { get; }

    public string ToolsDirectory { get; }

    public IReadOnlyList<LocalTool> Tools { get; }

    public IReadOnlyList<LocalToolProblem> Problems { get; }

    public bool IsEmpty => Tools.Count == 0;

    public static string LedgerKey(string toolName) =>
        LedgerKeyPrefix + toolName;

    public static bool IsValidName(string? name) =>
        !string.IsNullOrEmpty(name) && ValidName.IsMatch(name);

    /// <summary>
    /// Discovers the tools of the project rooted at
    /// <paramref name="projectRoot"/>. Never throws for a malformed tool
    /// folder; those are reported in <see cref="Problems"/>.
    /// </summary>
    public static LocalToolCatalog Discover(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        var root = Path.GetFullPath(projectRoot);
        var toolsDirectory = Path.Combine(root, ToolsDirectoryName);
        var tools = new List<LocalTool>();
        var problems = new List<LocalToolProblem>();

        if (Directory.Exists(toolsDirectory))
        {
            foreach (var folder in Directory
                         .EnumerateDirectories(toolsDirectory)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                var folderName = Path.GetFileName(folder);
                if (folderName.StartsWith('.'))
                {
                    continue;
                }

                var tool = TryLoad(folder, folderName, out var reason);
                if (tool is null)
                {
                    problems.Add(new LocalToolProblem(folderName, reason!));
                }
                else
                {
                    tools.Add(tool);
                }
            }
        }

        return new LocalToolCatalog(root, toolsDirectory, tools, problems);
    }

    /// <summary>
    /// Discovers the tools of the nearest project at or above
    /// <paramref name="directory"/>, or null when there is no project.
    /// </summary>
    public static LocalToolCatalog? DiscoverFrom(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)
            || !Directory.Exists(directory))
        {
            return null;
        }

        var current = new DirectoryInfo(directory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "effortless.json")))
            {
                return Discover(current.FullName);
            }

            current = current.Parent;
        }

        return null;
    }

    public LocalTool? TryGet(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return null;
        }

        return _tools.TryGetValue(rawName, out var tool) ? tool : null;
    }

    /// <summary>
    /// Matches a tool by its bare name, also accepting an "acct/name" form
    /// (the account prefix is meaningless for a local tool and ignored).
    /// </summary>
    public LocalTool? Match(string? rawName)
    {
        var direct = TryGet(rawName);
        if (direct is not null)
        {
            return direct;
        }

        var slash = rawName?.LastIndexOf('/') ?? -1;
        return slash > 0 && slash < rawName!.Length - 1
            ? TryGet(rawName[(slash + 1)..])
            : null;
    }

    private static LocalTool? TryLoad(
        string folder,
        string folderName,
        out string? reason)
    {
        reason = null;
        if (!IsValidName(folderName))
        {
            reason = "the folder name must be a lower-hyphen tool name ([a-z0-9][a-z0-9-]*)";
            return null;
        }

        string? description = null;
        var tags = new List<string>();
        string? runtimeName = null;
        string? entry = null;

        var manifestPath = Path.Combine(folder, ManifestFileName);
        if (File.Exists(manifestPath))
        {
            JObject manifest;
            try
            {
                manifest = JObject.Parse(File.ReadAllText(manifestPath));
            }
            catch (JsonException exception)
            {
                reason = $"tool.json is not valid JSON ({exception.Message})";
                return null;
            }

            var declaredName = manifest.Value<string>("name");
            if (!string.IsNullOrWhiteSpace(declaredName)
                && !string.Equals(declaredName, folderName, StringComparison.Ordinal))
            {
                reason = $"tool.json names '{declaredName}' but the folder is '{folderName}'";
                return null;
            }

            runtimeName = manifest.Value<string>("runtime");
            entry = manifest.Value<string>("entry");
            description = manifest.Value<string>("description");
            if (manifest["tags"] is JArray tagArray)
            {
                tags.AddRange(
                    tagArray.Values<string>()
                        .Where(tag => !string.IsNullOrWhiteSpace(tag))!);
            }
        }

        LocalToolRuntime runtime;
        if (!string.IsNullOrWhiteSpace(runtimeName))
        {
            switch (runtimeName.Trim().ToLowerInvariant())
            {
                case "dotnet":
                    runtime = LocalToolRuntime.Dotnet;
                    break;
                case "node":
                    runtime = LocalToolRuntime.Node;
                    break;
                case "script":
                    runtime = LocalToolRuntime.Script;
                    break;
                default:
                    reason = $"runtime '{runtimeName}' is not one of dotnet, node, script";
                    return null;
            }
        }
        else if (Directory.EnumerateFiles(folder, "*.csproj").Any())
        {
            runtime = LocalToolRuntime.Dotnet;
        }
        else if (File.Exists(Path.Combine(folder, "package.json")))
        {
            runtime = LocalToolRuntime.Node;
        }
        else if (Directory.EnumerateFiles(folder, "transpiler.*").Any())
        {
            runtime = LocalToolRuntime.Script;
        }
        else
        {
            reason = "no tool.json, *.csproj, package.json, or transpiler.* was found";
            return null;
        }

        entry = ResolveEntry(folder, runtime, entry, out reason);
        if (entry is null)
        {
            return null;
        }

        return new LocalTool(
            folderName,
            runtime,
            folder,
            entry,
            description,
            tags);
    }

    private static string? ResolveEntry(
        string folder,
        LocalToolRuntime runtime,
        string? declaredEntry,
        out string? reason)
    {
        reason = null;
        if (!string.IsNullOrWhiteSpace(declaredEntry))
        {
            var declared = Path.GetFullPath(Path.Combine(folder, declaredEntry));
            if (!File.Exists(declared))
            {
                reason = $"entry '{declaredEntry}' does not exist";
                return null;
            }

            return declared;
        }

        switch (runtime)
        {
            case LocalToolRuntime.Dotnet:
            {
                var projects = Directory.GetFiles(folder, "*.csproj");
                if (projects.Length == 1)
                {
                    return projects[0];
                }

                reason = projects.Length == 0
                    ? "a dotnet tool needs a *.csproj (or an explicit entry)"
                    : "more than one *.csproj; set entry in tool.json";
                return null;
            }

            case LocalToolRuntime.Node:
            {
                var packagePath = Path.Combine(folder, "package.json");
                string? main = null;
                if (File.Exists(packagePath))
                {
                    try
                    {
                        main = JObject.Parse(File.ReadAllText(packagePath))
                            .Value<string>("main");
                    }
                    catch (JsonException)
                    {
                        reason = "package.json is not valid JSON";
                        return null;
                    }
                }

                foreach (var candidate in new[] { main, "index.mjs", "index.js" })
                {
                    if (!string.IsNullOrWhiteSpace(candidate)
                        && File.Exists(Path.Combine(folder, candidate)))
                    {
                        return Path.GetFullPath(Path.Combine(folder, candidate));
                    }
                }

                reason = "a node tool needs package.json \"main\", index.mjs, or index.js (or an explicit entry)";
                return null;
            }

            default:
            {
                var scripts = Directory.GetFiles(folder, "transpiler.*");
                if (scripts.Length == 1)
                {
                    return scripts[0];
                }

                reason = scripts.Length == 0
                    ? "a script tool needs exactly one transpiler.* file (or an explicit entry)"
                    : "more than one transpiler.* file; set entry in tool.json";
                return null;
            }
        }
    }
}
