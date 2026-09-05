using Effortless.Cli.LocalTools;
using Xunit;

namespace Effortless.Cli.Tests;

public sealed class LocalToolCatalogTests
{
    [Fact(DisplayName = "unit-local-tool-discovery: discovery maps folders to runtimes and validates names")]
    public void DiscoveryMapsFoldersToRuntimesAndValidatesNames()
    {
        using var root = new TestDirectory();
        var tools = Path.Combine(root.Path, LocalToolCatalog.ToolsDirectoryName);
        Write(tools, "a/tool.json", """{ "runtime": "script", "entry": "run.sh", "description": "A", "tags": ["x", "y"] }""");
        Write(tools, "a/run.sh", "#!/bin/sh\n");
        Write(tools, "b/x.csproj", "<Project />");
        Write(tools, "c/package.json", """{ "main": "server.mjs" }""");
        Write(tools, "c/server.mjs", "");
        Write(tools, "d/transpiler.py", "print('hi')\n");
        Directory.CreateDirectory(Path.Combine(tools, "Bad_Name"));
        Directory.CreateDirectory(Path.Combine(tools, "e"));
        Write(tools, "f/tool.json", """{ "name": "not-f", "runtime": "script" }""");
        Write(tools, "f/transpiler.sh", "");
        Write(tools, "g/tool.json", """{ "runtime": "cobol" }""");
        Directory.CreateDirectory(Path.Combine(tools, ".hidden"));

        var catalog = LocalToolCatalog.Discover(root.Path);

        Assert.Equal(["a", "b", "c", "d"], catalog.Tools.Select(tool => tool.Name).ToArray());
        var a = catalog.TryGet("a")!;
        Assert.Equal(LocalToolRuntime.Script, a.Runtime);
        Assert.Equal(Path.GetFullPath(Path.Combine(tools, "a", "run.sh")), a.Entry);
        Assert.Equal("A", a.Description);
        Assert.Equal(["x", "y"], a.Tags);
        Assert.Equal(LocalToolRuntime.Dotnet, catalog.TryGet("b")!.Runtime);
        Assert.EndsWith("x.csproj", catalog.TryGet("b")!.Entry, StringComparison.Ordinal);
        Assert.Equal(LocalToolRuntime.Node, catalog.TryGet("c")!.Runtime);
        Assert.EndsWith("server.mjs", catalog.TryGet("c")!.Entry, StringComparison.Ordinal);
        Assert.Equal(LocalToolRuntime.Script, catalog.TryGet("d")!.Runtime);
        Assert.EndsWith("transpiler.py", catalog.TryGet("d")!.Entry, StringComparison.Ordinal);

        var problems = catalog.Problems.ToDictionary(problem => problem.Folder, problem => problem.Reason);
        Assert.Equal(["Bad_Name", "e", "f", "g"], problems.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Contains("lower-hyphen", problems["Bad_Name"], StringComparison.Ordinal);
        Assert.Contains("no tool.json", problems["e"], StringComparison.Ordinal);
        Assert.Contains("names 'not-f' but the folder is 'f'", problems["f"], StringComparison.Ordinal);
        Assert.Contains("runtime 'cobol'", problems["g"], StringComparison.Ordinal);
        Assert.Equal(
            "ERROR: 'g' under effortless-tools/ is not a valid local tool: runtime 'cobol' is not one of dotnet, node, script",
            catalog.Problems.Single(problem => problem.Folder == "g").Message);

        Assert.Equal("local-a", LocalToolCatalog.LedgerKey("a"));
        Assert.Equal("local-a", a.LedgerKey);
        Assert.Same(a, catalog.Match("someone/a"));
        Assert.Null(catalog.Match("nope"));
        Assert.True(LocalToolCatalog.IsValidName("rulebook-to-owl"));
        Assert.False(LocalToolCatalog.IsValidName("Rulebook"));
        Assert.False(LocalToolCatalog.IsValidName("-x"));
    }

    [Fact(DisplayName = "unit-local-tool-discovery-no-folder: a project without effortless-tools/ has an empty catalog")]
    public void MissingToolsFolderIsEmpty()
    {
        using var root = new TestDirectory();

        var catalog = LocalToolCatalog.Discover(root.Path);

        Assert.True(catalog.IsEmpty);
        Assert.Empty(catalog.Problems);
        Assert.Null(catalog.TryGet("anything"));
        Assert.Null(LocalToolCatalog.DiscoverFrom(root.Path));
    }

    private static void Write(string root, string relativePath, string contents)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }
}
