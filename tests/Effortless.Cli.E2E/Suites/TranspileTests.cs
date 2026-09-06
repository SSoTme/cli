using System.Text.Json;
using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

public sealed class TranspileTests
{
    [Fact(DisplayName = "tx-request-shape: request JSON and input FileSet match the legacy contract")]
    public async Task RequestJsonAndInputFileSetMatchLegacyContract()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server);
        server.IndexJson = index.Json;
        server.Enqueue("echo", ToolBehavior.Echo());
        using var sandbox = Sandbox.Create(cli);
        sandbox.SeedHome(index);
        sandbox.SeedProject("transpile");
        sandbox.WriteFile("in.txt", "hello wire");

        var result = await cli.Run(
            ["echo", "-i", "in.txt", "-o", "out.txt", "-p", "k=v", "extra1"],
            sandbox.ProjectPath,
            sandbox);
        var request = await server.WaitForRequestAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Stderr);
        Assert.Equal("echo", request.ToolName);
        Assert.Equal("v2026.01.01.0001", request.Version);

        Assert.Equal(["in.txt"], request.CliInput);
        Assert.Equal("out.txt", request.CliOutput);
        Assert.Equal(
            ["k=v", "param1=extra1", "project-name=wire-project", "a=1"],
            request.CliParams);
        Assert.Equal(180_000, request.CliWaitTimeout);
        Assert.False(request.CliDebug);
        Assert.Equal(string.Empty, request.CliJwt);

        var expectedTranspiler = SanitizeUrl(server.ToolUri("echo").ToString());
        Assert.Equal(expectedTranspiler, request.CliTranspiler);
        Assert.Equal(expectedTranspiler, request.TranspilerName);
        Assert.Equal(expectedTranspiler, request.TranspilerLowerHyphenName);

        var input = Assert.Single(request.InputFiles);
        Assert.Equal("in.txt", input.RelativePath);
        Assert.Equal(FileSetContentKind.ZippedFileContents, input.Kind);
        Assert.Equal("hello wire", input.Text);

        Assert.Equal(JsonValueKind.Object, GetRequired(request.Json, "settings").ValueKind);
        Assert.Empty(GetRequired(request.Json, "settings").EnumerateObject());
        Assert.Equal(
            string.Empty,
            GetRequired(request.Json, "cliInputFileContents").GetString());
        Assert.Equal(
            request.InputFileSetXml,
            GetRequired(request.Json, "cliInputFileSetXml").GetString());
        Assert.Equal(
            JsonValueKind.Null,
            GetRequired(request.Json, "cliInputFileSetJson").ValueKind);

        Assert.Contains(
            request.Json.EnumerateObject(),
            property => property.Name == "transpileRequest");
        Assert.Contains(
            GetRequired(request.Json, "transpileRequest").EnumerateObject(),
            property => property.Name == "zippedInputFileSet");
        server.ThrowIfFaulted();
    }

    [Fact(DisplayName = "tx-output-rules: output content and overwrite rules match legacy")]
    public async Task OutputContentAndOverwriteRulesMatchLegacy()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server);
        server.IndexJson = index.Json;
        var files = OutputFiles();
        server.Enqueue(
            "echo",
            ToolBehavior.Files(files),
            ToolBehavior.Files(files));
        using var sandbox = Sandbox.Create(cli);
        sandbox.SeedHome(index);
        sandbox.SeedProject("transpile");
        sandbox.WriteFile("in.txt", "input");
        sandbox.WriteFile("b.txt", "local edits");

        var first = await cli.Run(["echo", "-i", "in.txt"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, first.ExitCode);
        Assert.Empty(first.Stderr);
        Assert.Equal("alpha", sandbox.ReadFile("a.txt"));
        Assert.Equal("local edits", sandbox.ReadFile("b.txt"));
        Assert.Equal("charlie" + Environment.NewLine, sandbox.ReadFile("c.txt"));
        Assert.Equal(new byte[] { 0, 1, 2, 255 }, File.ReadAllBytes(Path.Combine(sandbox.ProjectPath, "d.bin")));
        Assert.Equal("echo", sandbox.ReadFile("e.txt"));
        Assert.Equal(new byte[] { 255, 2, 1, 0 }, File.ReadAllBytes(Path.Combine(sandbox.ProjectPath, "f.bin")));

        foreach (var path in new[] { "a.txt", "c.txt", "d.bin", "e.txt", "f.bin" })
        {
            Assert.Contains(
                $"[cli] Creating {CliReportedPath(Path.Combine(sandbox.ProjectPath, path))}",
                first.Stdout);
        }

        Assert.DoesNotContain(
            $"[cli] Creating {CliReportedPath(Path.Combine(sandbox.ProjectPath, "b.txt"))}",
            first.Stdout);

        var aPath = Path.Combine(sandbox.ProjectPath, "a.txt");
        var ePath = Path.Combine(sandbox.ProjectPath, "e.txt");
        var aWriteTime = File.GetLastWriteTimeUtc(aPath);
        var eWriteTime = File.GetLastWriteTimeUtc(ePath);
        await Task.Delay(1_100);

        // Isolate the writer's content comparison from the normal pre-write clean,
        // which deliberately removes Always-overwrite files before recreating them.
        var second = await cli.Run(
            ["echo", "-i", "in.txt", "-skipClean"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, second.ExitCode);
        Assert.Equal(aWriteTime, File.GetLastWriteTimeUtc(aPath));
        Assert.Equal(eWriteTime, File.GetLastWriteTimeUtc(ePath));
        Assert.DoesNotContain($"[cli] Creating {CliReportedPath(aPath)}", second.Stdout);
        Assert.DoesNotContain($"[cli] Creating {CliReportedPath(ePath)}", second.Stdout);
        Assert.Equal("local edits", sandbox.ReadFile("b.txt"));
        Assert.Equal(2, server.Requests.Count);
        server.ThrowIfFaulted();
    }

    private static FileSetEntry[] OutputFiles() =>
    [
        FileSetEntry.TextFile("a.txt", "alpha", alwaysOverwrite: true),
        FileSetEntry.TextFile("b.txt", "server copy", overwriteMode: "Never"),
        FileSetEntry.ZippedTextFile("c.txt", "charlie", alwaysOverwrite: true),
        FileSetEntry.BinaryFile("d.bin", [0, 1, 2, 255], alwaysOverwrite: true, zipped: true),
        FileSetEntry.TextFile("e.txt", "echo", overwriteMode: "Always"),
        FileSetEntry.BinaryFile("f.bin", [255, 2, 1, 0], alwaysOverwrite: true),
    ];

    private static JsonElement GetRequired(JsonElement element, string name)
    {
        Assert.True(element.TryGetProperty(name, out var value), $"Missing JSON property '{name}'.");
        return value;
    }

    private static string CliReportedPath(string path) =>
        OperatingSystem.IsMacOS() && path.StartsWith("/var/", StringComparison.Ordinal)
            ? "/private" + path
            : path;

    private static string SanitizeUrl(string value) =>
        value
            .Replace("/", string.Empty)
            .Replace(".", string.Empty)
            .Replace(":", string.Empty)
            .Replace("?", string.Empty)
            .Replace("&", string.Empty)
            .Replace("=", string.Empty)
            .Replace(" ", "-")
            .ToLowerInvariant();
}
