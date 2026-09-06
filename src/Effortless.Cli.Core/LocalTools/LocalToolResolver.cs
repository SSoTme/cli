#nullable enable
using Effortless.Cli.Options;

namespace Effortless.Cli.LocalTools;

/// <summary>
/// R12: finds a local tool for an invocation and provides its URL, starting
/// an ephemeral in-process host (once per CLI invocation, per project) when
/// no resident <c>effortless serve</c> is recorded in serve.json.
/// </summary>
public sealed class LocalToolResolver : IDisposable
{
    private readonly Dictionary<string, LocalToolHost> _ephemeralHosts =
        new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>
    /// The project root whose effortless-tools/ applies to
    /// <paramref name="invocation"/>: the loaded project, else the nearest
    /// project above the current directory.
    /// </summary>
    public static string? ProjectRootFor(CliInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        if (!string.IsNullOrWhiteSpace(invocation.Project?.RootPath))
        {
            return Path.GetFullPath(invocation.Project.RootPath);
        }

        var directory = invocation.CurrentDirectory;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        var current = new DirectoryInfo(directory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "effortless.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    public LocalTool? Find(CliInvocation invocation, string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return null;
        }

        var root = ProjectRootFor(invocation);
        return root is null
            ? null
            : LocalToolCatalog.Discover(root).Match(rawName);
    }

    /// <summary>
    /// The URL to POST to for <paramref name="tool"/>: the resident host when
    /// one is alive, otherwise this process's ephemeral host.
    /// </summary>
    public string UrlFor(LocalTool tool, bool debug)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var root = ProjectRootOf(tool);

        var live = ServeState.TryReadLive(root);
        if (live is not null)
        {
            if (debug)
            {
                Console.WriteLine(
                    $"DEBUG: reusing resident local tool host on port {live.Port} (pid {live.Pid})");
            }

            return $"http://127.0.0.1:{live.Port}/{tool.Name}";
        }

        if (!_ephemeralHosts.TryGetValue(root, out var host))
        {
            host = new LocalToolHost(
                LocalToolCatalog.Discover(root),
                debug: debug ? Console.WriteLine : null);
            host.Start();
            _ephemeralHosts[root] = host;
            if (debug)
            {
                Console.WriteLine(
                    $"[cli] Local tool host listening on {host.BaseUri} ({host.Catalog.Tools.Count} tool(s))");
            }
        }
        else if (host.Catalog.TryGet(tool.Name) is null)
        {
            host.Reload(LocalToolCatalog.Discover(root));
        }

        return host.ToolUri(tool.Name).ToString();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var host in _ephemeralHosts.Values)
        {
            host.Dispose();
        }

        _ephemeralHosts.Clear();
    }

    private static string ProjectRootOf(LocalTool tool) =>
        Path.GetFullPath(Path.Combine(tool.Directory, "..", ".."));
}
