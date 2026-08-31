using Effortless.Cli.Options;
using Effortless.Cli.Project;
using Effortless.Cli.Text;

namespace Effortless.Cli;

/// <summary>
/// Applies the CLI tool-resolution precedence rules to a mutable invocation.
/// Network refresh behavior is delegated to <see cref="RemoteToolsIndex"/>.
/// </summary>
public sealed class ToolResolver
{
    private readonly RemoteToolsIndex _remoteTools;

    public ToolResolver(RemoteToolsIndex remoteTools)
    {
        _remoteTools = remoteTools
            ?? throw new ArgumentNullException(nameof(remoteTools));
    }

    /// <summary>
    /// Populates URL, account, tool, and version metadata on
    /// <paramref name="invocation"/>. An unresolved name deliberately leaves
    /// <see cref="CliInvocation.TargetUrl"/> null.
    /// </summary>
    public void Resolve(CliInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        invocation.Options ??= new CliOptions();

        if (string.IsNullOrWhiteSpace(invocation.Account))
        {
            invocation.Account = invocation.Options.account ?? string.Empty;
        }

        // R1: an explicit target URL never consults either catalog.
        var explicitTarget = !string.IsNullOrWhiteSpace(
            invocation.Options.targetUrl)
            ? invocation.Options.targetUrl
            : invocation.TargetUrl;
        if (!string.IsNullOrWhiteSpace(explicitTarget))
        {
            ApplyDirectUrl(invocation, explicitTarget);
            return;
        }

        var rawName = GetRawToolName(invocation);
        invocation.RawTranspilerArg = rawName;
        if (string.IsNullOrWhiteSpace(rawName))
        {
            invocation.TargetUrl = null;
            return;
        }

        // R2: a URL in the tool position behaves exactly like -targetUrl.
        if (IsHttpUrl(rawName))
        {
            ApplyDirectUrl(invocation, rawName);
            return;
        }

        invocation.Transpiler = rawName;

        // Management commands consume the raw name themselves. Internal bridge
        // invocations skip the catalog but may still resolve their managed URL.
        if (IsManagementCommand(invocation.Options))
        {
            invocation.TargetUrl = null;
            return;
        }

        var localOverride = _remoteTools.TryGetToolUrl(rawName);
        RemoteToolResolution remote = null;
        if (!invocation.SkipRemoteToolsLookup)
        {
            if (string.IsNullOrWhiteSpace(localOverride)
                && !_remoteTools.EnsureFresh())
            {
                invocation.CatalogRefreshFailed = true;
                invocation.TargetUrl = null;
                return;
            }

            // A tool_urls-only tool must not cause a bridge call. Known catalog
            // tools are still resolved so version metadata survives R5.
            if (string.IsNullOrWhiteSpace(localOverride)
                || _remoteTools.ContainsTool(rawName))
            {
                var pinnedVersion = invocation.Options.latest
                    ? null
                    : FindHardPin(invocation, rawName);
                remote = _remoteTools.Resolve(
                    rawName,
                    pinnedVersion,
                    invocation.Options.latest);
                ApplyRemoteMetadata(invocation, remote);
            }
        }

        // R4/R5: the raw-name mapping wins even after a catalog match.
        if (!string.IsNullOrWhiteSpace(localOverride))
        {
            ApplyLocalOverride(invocation, rawName, localOverride, remote);
            return;
        }

        if (remote != null && !string.IsNullOrWhiteSpace(remote.Url))
        {
            invocation.TargetUrl = remote.Url;
            invocation.Transpiler =
                NameHelpers.SanitizeUrlForFilename(remote.Url);
            return;
        }

        // R4 also supports the internal bridge run, where the remote lookup is
        // intentionally disabled.
        var mappedUrl = _remoteTools.TryGetToolUrl(rawName);
        if (!string.IsNullOrWhiteSpace(mappedUrl))
        {
            ApplyLocalOverride(invocation, rawName, mappedUrl, remote);
            return;
        }

        // R6: retain an unresolved short name, but split a qualified account.
        invocation.TargetUrl = null;
        if (remote?.HasSpecificError != true)
        {
            ApplyAccountPrefix(invocation, rawName);
        }
    }

    private static void ApplyDirectUrl(
        CliInvocation invocation,
        string url)
    {
        invocation.TargetUrl = url;
        invocation.Transpiler = NameHelpers.SanitizeUrlForFilename(url);
        invocation.ResolvedVersionKey = null;
        invocation.ResolvedVersionUrl = null;
        invocation.ResolvedVersionLabel = null;
        invocation.ResolvedToolName = null;
        invocation.HasExplicitVersionError = false;
    }

    private static void ApplyRemoteMetadata(
        CliInvocation invocation,
        RemoteToolResolution resolution)
    {
        if (resolution == null)
        {
            return;
        }

        invocation.ResolvedVersionKey = resolution.VersionKey;
        invocation.ResolvedVersionUrl = resolution.Url;
        invocation.ResolvedVersionLabel = resolution.Label;
        invocation.ResolvedToolName = resolution.ToolName;
        invocation.HasExplicitVersionError =
            resolution.HasExplicitVersionError;
    }

    private static void ApplyLocalOverride(
        CliInvocation invocation,
        string rawName,
        string url,
        RemoteToolResolution remote)
    {
        invocation.TargetUrl = url;
        invocation.Transpiler = NameHelpers.SanitizeUrlForFilename(url);
        invocation.ResolvedVersionLabel = $"{rawName} [user-set]";
        invocation.HasExplicitVersionError =
            remote?.HasExplicitVersionError == true;

        // A pure local mapping has no remote metadata. R5 intentionally retains
        // metadata that was already resolved for a catalog-backed tool.
        if (remote == null)
        {
            invocation.ResolvedVersionUrl = url;
        }

        ApplyAccountPrefix(invocation, rawName, changeTranspiler: false);
    }

    private static string GetRawToolName(CliInvocation invocation)
    {
        if (!string.IsNullOrWhiteSpace(invocation.RawTranspilerArg))
        {
            return invocation.RawTranspilerArg;
        }

        if (!string.IsNullOrWhiteSpace(invocation.Transpiler))
        {
            return invocation.Transpiler;
        }

        return invocation.RemainingArguments?.FirstOrDefault();
    }

    private static string FindHardPin(
        CliInvocation invocation,
        string rawName)
    {
        var project = invocation.Project;
        if (project?.ProjectTranspilers == null)
        {
            return null;
        }

        var matches = project.ProjectTranspilers
            .Where(projectTranspiler =>
                ToolNameMatches(
                    FirstCommandToken(projectTranspiler.CommandLine),
                    rawName))
            .ToList();
        if (matches.Count == 0)
        {
            return null;
        }

        var selected = MatchCurrentDirectory(
            matches,
            project,
            invocation.CurrentDirectory)
            ?? matches[0];
        return selected.PinnedVersion;
    }

    private static ProjectTranspiler MatchCurrentDirectory(
        IReadOnlyList<ProjectTranspiler> matches,
        EffortlessProject project,
        string currentDirectory)
    {
        if (matches.Count < 2
            || string.IsNullOrWhiteSpace(project.RootPath)
            || string.IsNullOrWhiteSpace(currentDirectory))
        {
            return matches.FirstOrDefault();
        }

        string root;
        string current;
        try
        {
            root = Path.GetFullPath(project.RootPath)
                .Replace('\\', '/')
                .TrimEnd('/');
            current = Path.GetFullPath(currentDirectory)
                .Replace('\\', '/')
                .TrimEnd('/');
        }
        catch
        {
            return matches.FirstOrDefault();
        }

        if (!current.StartsWith(
                root,
                StringComparison.OrdinalIgnoreCase))
        {
            return matches.FirstOrDefault();
        }

        var relative = "/"
            + current.Substring(root.Length).TrimStart('/');
        return matches.FirstOrDefault(projectTranspiler =>
            string.Equals(
                NormalizeRelativePath(projectTranspiler.RelativePath),
                NormalizeRelativePath(relative),
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool ToolNameMatches(
        string commandTool,
        string requestedTool)
    {
        if (string.IsNullOrWhiteSpace(commandTool)
            || string.IsNullOrWhiteSpace(requestedTool))
        {
            return false;
        }

        var commandBase = StripVersionSuffix(commandTool);
        var requestedBase = StripVersionSuffix(requestedTool);
        if (string.Equals(
                commandBase,
                requestedBase,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(
            ShortName(commandBase),
            ShortName(requestedBase),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyAccountPrefix(
        CliInvocation invocation,
        string rawName,
        bool changeTranspiler = true)
    {
        var toolName = StripVersionSuffix(rawName);
        var slash = toolName.IndexOf('/');
        if (slash <= 0)
        {
            return;
        }

        invocation.Account = toolName.Substring(0, slash);
        if (changeTranspiler)
        {
            invocation.Transpiler = toolName.Substring(slash + 1);
        }
    }

    private static bool IsManagementCommand(CliOptions options)
    {
        return options.listVersions
            || options.refreshTools
            || options.listTools
            || !string.IsNullOrWhiteSpace(options.searchTools)
            || options.listUrls
            || options.version
            || options.init
            || options.uninstall
            || options.authenticate
            || options.projectLogin
            || options.logout
            || options.subscription
            || options.info
            || options.help
            || options.upgrade
            || options.upgradeCli
            || options.upgradeAll
            || !string.IsNullOrWhiteSpace(options.viewUrl)
            || !string.IsNullOrWhiteSpace(options.setUrl)
            || !string.IsNullOrWhiteSpace(options.removeUrl);
    }

    private static bool IsHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp
                || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static string FirstCommandToken(string commandLine)
    {
        return commandLine?
            .Split(
                new[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
    }

    private static string StripVersionSuffix(string toolName)
    {
        var slash = toolName.LastIndexOf('/');
        if (slash < 0 || slash == toolName.Length - 1)
        {
            return toolName;
        }

        var last = toolName.Substring(slash + 1);
        return last.Length > 1
            && (last[0] == 'v' || last[0] == 'V')
            && char.IsDigit(last[1])
                ? toolName.Substring(0, slash)
                : toolName;
    }

    private static string ShortName(string toolName)
    {
        var slash = toolName.LastIndexOf('/');
        return slash < 0
            ? toolName
            : toolName.Substring(slash + 1);
    }

    private static string NormalizeRelativePath(string path)
    {
        return "/" + (path ?? string.Empty)
            .Replace('\\', '/')
            .Trim('/');
    }
}
