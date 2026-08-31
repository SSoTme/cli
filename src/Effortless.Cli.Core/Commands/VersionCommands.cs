using Effortless.Cli.Options;
using Effortless.Cli.Project;

namespace Effortless.Cli.Commands;

public sealed class VersionCommands
{
    private readonly RemoteToolsIndex _index;

    public VersionCommands(RemoteToolsIndex index)
    {
        _index = index;
    }

    public int Refresh(bool debug = false)
    {
        var cachedVersion = _index.CliVersionFile.Exists
            ? File.ReadAllText(_index.CliVersionFile.FullName).Trim()
            : null;
        Delete(_index.IndexFile, debug);
        Delete(_index.CliVersionFile, debug);
        Delete(_index.BridgeVersionIndexFile, debug);
        CliLog.LogLine("Refreshing remote tools index...");
        var succeeded = _index.Refresh(
            "RefreshRemoteTools: explicit -refreshTools invocation",
            $"User invoked -refreshTools; deleted ssotme-tools.json, cli_version, and bridge_version_index to force a full re-fetch. cached CLI version was '{cachedVersion ?? "(none)"}', current is '{_index.CliVersion}'.");
        if (!succeeded)
        {
            WriteError(
                $"ERROR: Remote tools index refresh failed: {_index.LastRefreshError}");
            return -1;
        }

        CliLog.LogLine("Remote tools index refreshed.");
        return 0;
    }

    public int ListVersions(string toolName)
    {
        var versions = _index.ListVersions(toolName);
        if (versions is null)
        {
            Console.WriteLine(
                $"No versions for '{toolName}' were found in the remote tools index. It may still exist in our legacy system.");
            return 0;
        }

        Console.WriteLine(
            $"Available versions for {versions.ToolName}:");
        Console.WriteLine();
        foreach (var version in versions.Versions)
        {
            Console.WriteLine(
                $"  {version.VersionKey}{(version.IsHead ? " (latest)" : string.Empty)}");
            Console.WriteLine($"    url: {version.Url}");
        }

        var overrideUrl =
            Config.ToolUrls.TryGetUrlFromFileUrls(toolName);
        if (!string.IsNullOrEmpty(overrideUrl))
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  * globally overridden via effortless -setUrl {toolName}={overrideUrl}");
            Console.WriteLine(
                $"    run 'effortless -removeUrl {toolName}' to reset");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Run with: effortless {toolName}/<versionKey>");
        Console.WriteLine($"Run latest: effortless {toolName}");
        return 0;
    }

    public int Upgrade(CliInvocation invocation, bool all)
    {
        var refreshResult = Refresh(invocation.Options.debug);
        if (refreshResult != 0)
        {
            return refreshResult;
        }

        var project = invocation.Project
                      ?? ProjectLocator.TryToLoad(
                          new DirectoryInfo(
                              invocation.CurrentDirectory));
        if (project is null)
        {
            WriteError(
                "No effortless.json project found in this directory.");
            return 0;
        }

        var toolName = invocation.RawTranspilerArg
                       ?? invocation.Transpiler;
        if (all || string.IsNullOrEmpty(toolName))
        {
            return UpgradeAll(project);
        }

        var head = _index.Resolve(toolName);
        if (head is null || string.IsNullOrEmpty(head.Url))
        {
            WriteError(
                $"Could not resolve tool '{toolName}' from the remote tools index.");
            return 0;
        }

        var matches = project.ProjectTranspilers
            .Where(step => EffortlessProject.ToolNameMatches(
                EffortlessProject.GetToolName(step.CommandLine),
                toolName))
            .ToList();
        var matched = MatchCurrentDirectory(
            matches,
            project,
            invocation.CurrentDirectory);
        if (matched is null)
        {
            Console.WriteLine(
                $"{toolName} is not used in this project — nothing to unpin here.");
            Console.WriteLine(
                $"Refreshed the core tools index; '{toolName}' will track latest (HEAD {head.VersionKey}) wherever it is used unpinned.");
            return 0;
        }

        var oldVersion = matched.PinnedVersion
                         ?? matched.LastVersionUsed
                         ?? "(unpinned)";
        matched.PinnedVersion = null;
        matched.LastVersionUsed = head.VersionKey;
        project.Save();
        Console.WriteLine(
            $"Upgraded {toolName}: {oldVersion} → HEAD ({head.VersionKey}, unpinned — will track latest)");
        return 0;
    }

    public int ListTools(string search = null)
    {
        var tools = _index.ListTools(search);
        if (!string.IsNullOrEmpty(search) && tools.Count == 0)
        {
            Console.WriteLine($"No tools matched '{search}'.");
            return 0;
        }

        Console.WriteLine(
            string.IsNullOrEmpty(search)
                ? $"Available tools ({tools.Count}):"
                : $"Tools matching '{search}' ({tools.Count}):");
        Console.WriteLine();
        foreach (var tool in tools)
        {
            Console.WriteLine(
                $"  {tool.CanonicalName}  {tool.HeadVersion ?? "NO HEAD"}");
        }

        return 0;
    }

    private int UpgradeAll(EffortlessProject project)
    {
        if (project.ProjectTranspilers.Count == 0)
        {
            Console.WriteLine(
                "No transpilers found in effortless.json.");
            return 0;
        }

        var plan = new ProjectToolFreshness(_index).Plan(
            project,
            MissingProjectToolPolicy.Skip);
        var changedCount = plan.ChangedCount;
        var missingCount = plan.MissingCount;
        foreach (var entry in plan.Entries)
        {
            if (entry.IsMissing)
            {
                Console.WriteLine(
                    $"  SKIP {entry.ToolName} — not found in remote tools index");
                continue;
            }

            if (!entry.NeedsChange)
            {
                Console.WriteLine(
                    $"  OK   {entry.ToolName} — already unpinned at HEAD ({entry.HeadVersion})");
                continue;
            }

            Console.WriteLine(
                $"  UP   {entry.ToolName}: {entry.PreviousVersion} → HEAD ({entry.HeadVersion}, unpinned)");
        }

        plan.Apply(project);
        Console.WriteLine(
            $"\nUpgraded {changedCount} tool(s)"
            + (missingCount > 0
                ? $", skipped {missingCount}"
                : string.Empty)
            + ".");
        return 0;
    }

    private static ProjectTranspiler MatchCurrentDirectory(
        IReadOnlyList<ProjectTranspiler> matches,
        EffortlessProject project,
        string currentDirectory)
    {
        if (matches.Count == 0)
        {
            return null;
        }

        if (matches.Count == 1)
        {
            return matches[0];
        }

        var relative = project.GetProjectRelativePath(
            currentDirectory);
        return matches.FirstOrDefault(
                   step => string.Equals(
                       step.RelativePath,
                       relative,
                       StringComparison.OrdinalIgnoreCase))
               ?? matches[0];
    }

    private static void Delete(FileInfo file, bool debug)
    {
        file.Refresh();
        if (!file.Exists)
        {
            return;
        }

        file.Delete();
        if (debug)
        {
            Console.WriteLine($"DEBUG: deleted {file.FullName}");
        }
    }

    private static void WriteError(string message)
    {
        var color = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ForegroundColor = color;
    }
}
