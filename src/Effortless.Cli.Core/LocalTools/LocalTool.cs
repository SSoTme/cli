#nullable enable
namespace Effortless.Cli.LocalTools;

/// <summary>
/// The three tool shapes a local tool folder can declare (step 12 / D30).
/// </summary>
public enum LocalToolRuntime
{
    /// <summary>
    /// A .NET web app built on the same tool-side contract every published
    /// cloud tool uses: started with <c>dotnet run --project</c>, reads
    /// <c>PORT</c>, answers <c>POST /</c>. The host proxies to it.
    /// </summary>
    Dotnet,

    /// <summary>
    /// A node program that serves the fileset contract (normally through the
    /// shipped <c>lib/fileset-handler.mjs</c>): started with <c>node</c>,
    /// reads <c>PORT</c>, answers <c>POST /</c>. The host proxies to it.
    /// </summary>
    Node,

    /// <summary>
    /// Any executable or interpreted file. The host does the fileset work and
    /// hands the tool input/output directories through the
    /// <c>EFFORTLESS_*</c> environment contract.
    /// </summary>
    Script,
}

/// <summary>
/// One discovered <c>effortless-tools/&lt;name&gt;/</c> folder.
/// </summary>
public sealed record LocalTool(
    string Name,
    LocalToolRuntime Runtime,
    string Directory,
    string Entry,
    string? Description,
    IReadOnlyList<string> Tags)
{
    /// <summary>
    /// The ledger (.zfs) key: stable across host ports so clean and purge
    /// find the same file a build wrote.
    /// </summary>
    public string LedgerKey => LocalToolCatalog.LedgerKey(Name);

    public string RuntimeName => Runtime switch
    {
        LocalToolRuntime.Dotnet => "dotnet",
        LocalToolRuntime.Node => "node",
        _ => "script",
    };
}

/// <summary>
/// A folder under <c>effortless-tools/</c> that discovery could not turn
/// into a tool, with the reason it was skipped.
/// </summary>
public sealed record LocalToolProblem(string Folder, string Reason)
{
    public string Message =>
        $"ERROR: '{Folder}' under effortless-tools/ is not a valid local tool: {Reason}";
}
