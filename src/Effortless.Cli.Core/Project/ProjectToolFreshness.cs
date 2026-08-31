namespace Effortless.Cli.Project;

public enum MissingProjectToolPolicy
{
    Fail,
    Skip,
}

/// <summary>
/// Resolves project tools to catalog HEAD before applying any version changes.
/// </summary>
public sealed class ProjectToolFreshness
{
    private readonly RemoteToolsIndex _index;

    public ProjectToolFreshness(RemoteToolsIndex index)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
    }

    public ProjectToolUpgradePlan Plan(
        EffortlessProject project,
        MissingProjectToolPolicy missingToolPolicy)
    {
        ArgumentNullException.ThrowIfNull(project);

        var entries = new List<ProjectToolUpgradeEntry>();
        foreach (var step in project.ProjectTranspilers
                     ?? Enumerable.Empty<ProjectTranspiler>())
        {
            var tool = EffortlessProject.GetToolName(step.CommandLine);
            if (IsExcluded(tool))
            {
                continue;
            }

            var head = _index.ResolveHead(tool);
            if (head is null
                || head.HasSpecificError
                || string.IsNullOrWhiteSpace(head.Url)
                || string.IsNullOrWhiteSpace(head.VersionKey))
            {
                if (HasCustomUrl(tool))
                {
                    continue;
                }

                entries.Add(
                    ProjectToolUpgradeEntry.Missing(step, tool));
                if (missingToolPolicy == MissingProjectToolPolicy.Fail)
                {
                    return ProjectToolUpgradePlan.Failed(
                        entries,
                        $"Project tool '{tool}' is missing from the current remote tools index or has no HEAD version.");
                }

                continue;
            }

            entries.Add(
                ProjectToolUpgradeEntry.Resolved(step, tool, head));
        }

        return ProjectToolUpgradePlan.Succeeded(entries);
    }

    private bool HasCustomUrl(string tool)
    {
        var direct = _index.TryGetToolUrl(tool);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return true;
        }

        var slash = tool.LastIndexOf('/');
        return slash >= 0
            && !string.IsNullOrWhiteSpace(
                _index.TryGetToolUrl(tool[(slash + 1)..]));
    }

    private static bool IsExcluded(string tool)
    {
        if (string.IsNullOrWhiteSpace(tool)
            || tool.Equals("-execute", StringComparison.OrdinalIgnoreCase)
            || tool.Equals("-exec", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(tool, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https";
    }
}

public sealed class ProjectToolUpgradePlan
{
    private ProjectToolUpgradePlan(
        bool succeeded,
        IReadOnlyList<ProjectToolUpgradeEntry> entries,
        string error)
    {
        IsSuccessful = succeeded;
        Entries = entries;
        Error = error;
    }

    public bool IsSuccessful { get; }

    public IReadOnlyList<ProjectToolUpgradeEntry> Entries { get; }

    public string Error { get; }

    public int ChangedCount =>
        Entries.Count(entry => entry.NeedsChange);

    public int MissingCount =>
        Entries.Count(entry => entry.IsMissing);

    public bool Apply(EffortlessProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!IsSuccessful)
        {
            return false;
        }

        var changed = false;
        foreach (var entry in Entries.Where(entry => entry.NeedsChange))
        {
            entry.Step.ClearCommandLineVersion();
            entry.Step.PinnedVersion = null;
            entry.Step.LastVersionUsed = entry.HeadVersion;
            changed = true;
        }

        if (changed)
        {
            project.Save();
        }

        return changed;
    }

    internal static ProjectToolUpgradePlan Succeeded(
        IReadOnlyList<ProjectToolUpgradeEntry> entries) =>
        new(true, entries, null);

    internal static ProjectToolUpgradePlan Failed(
        IReadOnlyList<ProjectToolUpgradeEntry> entries,
        string error) =>
        new(false, entries, error);
}

public sealed class ProjectToolUpgradeEntry
{
    private ProjectToolUpgradeEntry(
        ProjectTranspiler step,
        string toolName,
        string headVersion,
        bool isMissing)
    {
        Step = step;
        ToolName = toolName;
        HeadVersion = headVersion;
        IsMissing = isMissing;
    }

    public ProjectTranspiler Step { get; }

    public string ToolName { get; }

    public string HeadVersion { get; }

    public bool IsMissing { get; }

    public bool NeedsChange =>
        !IsMissing
        && (Step.HasCommandLineVersion()
            || !string.IsNullOrWhiteSpace(Step.PinnedVersion)
            || !string.Equals(
                Step.LastVersionUsed,
                HeadVersion,
                StringComparison.Ordinal));

    public string PreviousVersion =>
        Step.PinnedVersion
        ?? Step.LastVersionUsed
        ?? "(unpinned)";

    internal static ProjectToolUpgradeEntry Missing(
        ProjectTranspiler step,
        string toolName) =>
        new(step, toolName, null, true);

    internal static ProjectToolUpgradeEntry Resolved(
        ProjectTranspiler step,
        string toolName,
        RemoteToolResolution head) =>
        new(step, toolName, head.VersionKey, false);
}
