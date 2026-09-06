using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

/// <summary>
/// Step 12: project-local tools under effortless-tools/, the local transpiler
/// host (ephemeral and resident), and the three runtime shapes.
/// </summary>
public sealed class LocalToolTests
{
    private const string Fixture = "project-local-tools";

    [Fact(DisplayName = "local-tool-resolves-before-catalog: a local tool name resolves without any catalog or bridge call")]
    public async Task LocalToolResolvesWithoutCatalog()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = SeedProject(cli, server);

        var result = await cli.Run(["echo-params", "-input", "README.md"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("cli:> echo-params [local]", result.Stdout, StringComparison.Ordinal);
        Assert.Empty(server.Requests);
        var output = sandbox.ReadFile("echo.txt");
        Assert.Contains("tool=echo-params", output, StringComparison.Ordinal);
        Assert.Contains("  hello local tools", output, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "local-tool-build-writes-output: a build step naming a local tool writes its output through the normal SaveFileSet path")]
    public async Task BuildStepWritesOutput()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = SeedProject(cli, server);
        AddStep(sandbox, "echo-params -input README.md -output echo.txt");

        var build = await cli.Run(["build"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, build.ExitCode);
        Assert.Contains("cli:> echo-params [local]", build.Stdout, StringComparison.Ordinal);
        Assert.Contains("output=echo.txt", sandbox.ReadFile("echo.txt"), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(sandbox.ProjectPath, ".effortless", "local-echo-params.zfs")));
        Assert.Empty(server.Requests);
        // Local tools are not catalog-versioned: nothing is pinned or recorded.
        var step = sandbox.ProjectFile["ProjectTranspilers"]!.AsArray().Single()!.AsObject();
        Assert.False(step.ContainsKey("LastUrl"));
        Assert.False(step.ContainsKey("PinnedVersion"));
    }

    [Fact(DisplayName = "local-tool-ledger-and-clean: clean removes exactly what the local tool wrote, keyed local-<name>")]
    public async Task CleanRemovesExactlyTheLedgeredFiles()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = SeedProject(cli, server);
        AddStep(sandbox, "echo-params -input README.md -output echo.txt");
        sandbox.WriteFile("untouched.txt", "mine");

        var build = await cli.Run(["build"], sandbox.ProjectPath, sandbox);
        Assert.Equal(0, build.ExitCode);
        Assert.True(File.Exists(Path.Combine(sandbox.ProjectPath, "echo.txt")));

        var clean = await cli.Run(["clean"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, clean.ExitCode);
        Assert.Contains("Cleaning", clean.Stdout, StringComparison.Ordinal);
        Assert.Contains("echo.txt", clean.Stdout, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, "echo.txt")));
        Assert.True(File.Exists(Path.Combine(sandbox.ProjectPath, "untouched.txt")));
        Assert.True(File.Exists(Path.Combine(sandbox.ProjectPath, "README.md")));
        Assert.Empty(server.Requests);
    }

    [Fact(DisplayName = "local-tool-ephemeral-host-lifecycle: a build with no serve.json starts an in-process host and leaves nothing behind")]
    public async Task EphemeralHostLeavesNothingBehind()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = SeedProject(cli, server);
        AddStep(sandbox, "echo-params -input README.md -output echo.txt");

        var build = await cli.Run(["build", "-debug"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, build.ExitCode);
        var listening = build.Stdout
            .Split('\n')
            .Single(line => line.Contains("Local tool host listening on http://127.0.0.1:", StringComparison.Ordinal));
        var port = int.Parse(listening.Split(':')[^1].Split('/')[0]);
        Assert.Contains("DEBUG: POST http://127.0.0.1:" + port + "/echo-params", build.Stdout, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, ".effortless", "serve.json")));
        Assert.False(await IsListening(port), "the ephemeral host must stop with the CLI");
    }

    [Fact(DisplayName = "local-tool-resident-serve-reuse: a running serve is reused by builds via serve.json")]
    [Trait("Slow", "true")]
    public async Task ResidentServeIsReusedByBuilds()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = SeedProject(cli, server);
        AddStep(sandbox, "echo-params -input README.md -output echo.txt");
        var servePath = Path.Combine(sandbox.ProjectPath, ".effortless", "serve.json");

        using var serve = BackgroundCli.Start(
            cli,
            ["serve"],
            sandbox.ProjectPath,
            sandbox,
            new Dictionary<string, string> { ["EFFORTLESS_SERVE_EXIT_AFTER_MS"] = "20000" });
        await serve.WaitUntilAsync(() => File.Exists(servePath) && serve.Stdout.Contains("Press Ctrl+C", StringComparison.Ordinal));
        var state = JsonNode.Parse(File.ReadAllText(servePath))!.AsObject();
        Assert.Equal(serve.Pid, state["pid"]!.GetValue<int>());
        var port = state["port"]!.GetValue<int>();

        var build = await cli.Run(["build", "-debug"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, build.ExitCode);
        Assert.Contains($"DEBUG: reusing resident local tool host on port {port}", build.Stdout, StringComparison.Ordinal);
        Assert.Contains($"DEBUG: POST http://127.0.0.1:{port}/echo-params", build.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("Local tool host listening", build.Stdout, StringComparison.Ordinal);
        Assert.Contains("output=echo.txt", sandbox.ReadFile("echo.txt"), StringComparison.Ordinal);
        await serve.WaitUntilAsync(() => serve.Stdout.Contains("[cli] POST /echo-params (script)", StringComparison.Ordinal));

        await serve.WaitForExitAsync();
        Assert.Contains("[cli] Local tool host stopped.", serve.Stdout, StringComparison.Ordinal);
        Assert.False(File.Exists(servePath));
    }

    [Fact(DisplayName = "local-tool-params-passthrough: cliParams and -output reach a script tool via the env contract")]
    public async Task ParamsAndOutputReachTheScript()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = SeedProject(cli, server);

        var result = await cli.Run(
            ["echo-params", "-input", "README.md", "-output", "out/echo.txt", "-p", "schema=public", "extra"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        var output = sandbox.ReadFile("out/echo.txt");
        Assert.Contains("tool=echo-params", output, StringComparison.Ordinal);
        Assert.Contains("output=out/echo.txt", output, StringComparison.Ordinal);
        Assert.Contains("\"schema=public\"", output, StringComparison.Ordinal);
        Assert.Contains("\"param1=extra\"", output, StringComparison.Ordinal);
        Assert.Contains("inputs:\n  README.md", output.Replace("\r\n", "\n"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "local-tool-script-overwrite-modes: a script's files obey the protocol's overwrite rules exactly")]
    public async Task ScriptFilesObeyOverwriteModes()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = SeedProject(cli, server);
        string Read(string path) => sandbox.ReadFile(path).Replace("\r\n", "\n");

        var first = await cli.Run(["overwrite-modes", "-input", "README.md", "-p", "stamp=one"], sandbox.ProjectPath, sandbox);
        Assert.Equal(0, first.ExitCode);
        Assert.Equal("always one\n", Read("always.txt"));
        Assert.Equal("never one\n", Read("never.txt"));
        Assert.Equal("plain one\n", Read("plain.txt"));
        Assert.Equal("gen one\n", Read("sql/01-tables.sql"));
        Assert.Equal("seam one\n", Read("sql/01b-customize-schema.sql"));
        Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, "effortless-overwrite-modes.json")), "the declaration is not an output file");

        var second = await cli.Run(["overwrite-modes", "-input", "README.md", "-p", "stamp=two"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, second.ExitCode);
        Assert.Equal("always two\n", Read("always.txt"));
        Assert.Equal("never one\n", Read("never.txt"));
        // Undeclared = no OverwriteMode node = the protocol default: written once, never touched again.
        Assert.Equal("plain one\n", Read("plain.txt"));
        Assert.Equal("gen two\n", Read("sql/01-tables.sql"));
        Assert.Equal("seam one\n", Read("sql/01b-customize-schema.sql"));
    }

    [Fact(DisplayName = "local-tool-nonzero-exit-fails-step: a script that exits non-zero fails the step with its log")]
    public async Task NonZeroExitFailsTheStep()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = SeedProject(cli, server);

        var result = await cli.Run(["fail-tool", "-input", "README.md"], sandbox.ProjectPath, sandbox);

        Assert.True(result.Failed);
        Assert.Contains("fail-tool: about to fail", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("ERROR: Local tool 'fail-tool' exited with code 3.", result.Stdout, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, "never.txt")));
    }

    [Fact(DisplayName = "local-tool-missing-runtime: a tool.json naming an unknown runtime is a clear error")]
    public async Task UnknownRuntimeIsAClearError()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = SeedProject(cli, server);
        sandbox.WriteFile("effortless-tools/bogus/tool.json", """{ "runtime": "cobol" }""");

        var serve = await cli.Run(["serve"], sandbox.ProjectPath, sandbox, timeoutMs: 60_000, environment: new Dictionary<string, string> { ["EFFORTLESS_SERVE_EXIT_AFTER_MS"] = "500" });
        var direct = await cli.Run(["bogus", "-input", "README.md"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, serve.ExitCode);
        Assert.Contains(
            "ERROR: 'bogus' under effortless-tools/ is not a valid local tool: runtime 'cobol' is not one of dotnet, node, script",
            serve.Stdout,
            StringComparison.Ordinal);
        Assert.Contains("(5 tool(s))", serve.Stdout, StringComparison.Ordinal);
        // An invalid folder is not a tool, so the name falls through to the catalog and is not found there.
        Assert.True(direct.Failed);
        Assert.Contains("Tool 'bogus' does not exist.", direct.Stdout, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "local-tool-node-handler-roundtrip: a node tool built on lib/fileset-handler.mjs round-trips input to output")]
    [Trait("Slow", "true")]
    public async Task NodeHandlerRoundTrips()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = SeedProject(cli, server);

        var result = await cli.Run(
            ["to-upper-node", "-input", "README.md", "-p", "extra=from-params", "-debug"],
            sandbox.ProjectPath,
            sandbox,
            timeoutMs: 180_000);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("cli:> to-upper-node [local]", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("DEBUG: started node tool 'to-upper-node'", result.Stdout, StringComparison.Ordinal);
        Assert.Matches("to-upper-node: 1 input file\\(s\\), PORT=\\d+", result.Stdout);
        Assert.Equal("HELLO LOCAL TOOLS\n", sandbox.ReadFile("Output.txt").Replace("\r\n", "\n"));
        Assert.Equal("from-params", sandbox.ReadFile("extra.txt"));
        Assert.True(File.Exists(Path.Combine(sandbox.ProjectPath, ".effortless", "local-tools", "fileset-handler.mjs")));
        Assert.True(File.Exists(Path.Combine(sandbox.ProjectPath, ".effortless", "local-to-upper-node.zfs")));
    }

    [Fact(DisplayName = "local-tool-dotnet-same-shape-as-cloud: a dotnet tool reading PORT and answering POST / works unchanged as a local tool")]
    [Trait("Slow", "true")]
    public async Task DotnetToolHasTheCloudShape()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = SeedProject(cli, server);

        var result = await cli.Run(
            ["to-upper-dotnet", "-input", "README.md", "-output", "Upper.txt", "-debug"],
            sandbox.ProjectPath,
            sandbox,
            timeoutMs: 300_000);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("cli:> to-upper-dotnet [local]", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("DEBUG: started dotnet tool 'to-upper-dotnet'", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("to-upper-dotnet saw 1 param(s)", result.Stdout, StringComparison.Ordinal);
        Assert.Equal("HELLO LOCAL TOOLS\n", sandbox.ReadFile("Upper.txt").Replace("\r\n", "\n"));
    }

    [Fact(DisplayName = "local-tool-seturl-override-wins: a tool_urls.json mapping beats a same-named local tool")]
    public async Task ToolUrlOverrideWins()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = SeedProject(cli, server);
        server.Enqueue("echo-params", ToolBehavior.Files(FileSetEntry.TextFile("from-mock.txt", "mock", alwaysOverwrite: true)));

        var set = await cli.Run(
            ["-setToolUrl", $"echo-params={server.ToolUri("echo-params")}"],
            sandbox.ProjectPath,
            sandbox);
        var run = await cli.Run(["echo-params", "-input", "README.md", "-debug"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, set.ExitCode);
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("cli:> echo-params [user-set]", run.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("[local]", run.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("Local tool host listening", run.Stdout, StringComparison.Ordinal);
        Assert.Single(server.Requests);
        Assert.Equal("mock", sandbox.ReadFile("from-mock.txt"));
        Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, "echo.txt")));
    }

    [Fact(DisplayName = "serve-lists-tools: serve prints one URL per tool and GET / lists them")]
    [Trait("Slow", "true")]
    public async Task ServeListsTools()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = SeedProject(cli, server);
        var port = FreePort();
        var servePath = Path.Combine(sandbox.ProjectPath, ".effortless", "serve.json");

        using var serve = BackgroundCli.Start(
            cli,
            ["serve", "-port", port.ToString()],
            sandbox.ProjectPath,
            sandbox,
            new Dictionary<string, string> { ["EFFORTLESS_SERVE_EXIT_AFTER_MS"] = "20000" });
        await serve.WaitUntilAsync(() => serve.Stdout.Contains("Press Ctrl+C to stop.", StringComparison.Ordinal));

        Assert.Contains($"[cli] Local tool host listening on http://127.0.0.1:{port}/ (5 tool(s))", serve.Stdout, StringComparison.Ordinal);
        Assert.Contains($"  echo-params  http://127.0.0.1:{port}/echo-params  (script)", serve.Stdout, StringComparison.Ordinal);
        Assert.Contains($"  to-upper-node  http://127.0.0.1:{port}/to-upper-node  (node)", serve.Stdout, StringComparison.Ordinal);
        Assert.Contains($"  to-upper-dotnet  http://127.0.0.1:{port}/to-upper-dotnet  (dotnet)", serve.Stdout, StringComparison.Ordinal);
        Assert.True(File.Exists(servePath));

        using var http = new HttpClient();
        var listing = await http.GetFromJsonAsync<JsonObject>($"http://127.0.0.1:{port}/");
        var names = listing!["tools"]!.AsArray().Select(tool => tool!["name"]!.GetValue<string>()).ToArray();
        Assert.Equal(["echo-params", "fail-tool", "overwrite-modes", "to-upper-dotnet", "to-upper-node"], names);
        var health = await http.GetFromJsonAsync<JsonObject>($"http://127.0.0.1:{port}/echo-params");
        Assert.Equal("healthy", health!["status"]!.GetValue<string>());

        await serve.WaitForExitAsync();
        Assert.Contains("[cli] Local tool host stopped.", serve.Stdout, StringComparison.Ordinal);
        Assert.False(File.Exists(servePath));
    }

    [Fact(DisplayName = "serve-port-fixed: serve -port binds the given loopback port")]
    [Trait("Slow", "true")]
    public async Task ServeBindsTheRequestedPort()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = SeedProject(cli, server);
        var port = FreePort();

        using var serve = BackgroundCli.Start(
            cli,
            ["serve", "-port", port.ToString()],
            sandbox.ProjectPath,
            sandbox,
            new Dictionary<string, string> { ["EFFORTLESS_SERVE_EXIT_AFTER_MS"] = "20000" });
        await serve.WaitUntilAsync(() => serve.Stdout.Contains("Press Ctrl+C to stop.", StringComparison.Ordinal));

        Assert.Contains($"listening on http://127.0.0.1:{port}/", serve.Stdout, StringComparison.Ordinal);
        Assert.True(await IsListening(port));
        var state = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.ProjectPath, ".effortless", "serve.json")))!.AsObject();
        Assert.Equal(port, state["port"]!.GetValue<int>());
        await serve.WaitForExitAsync();
        Assert.False(await IsListening(port));
    }

    [Fact(DisplayName = "serve-no-tools: serve without effortless-tools/ says where it looked")]
    public async Task ServeWithoutToolsExplains()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = Sandbox.Create(cli);
        sandbox.SeedHome(IndexFixture.Load(server));
        sandbox.SeedProject("project-basic");

        var result = await cli.Run(["serve"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        // macOS reports /var/folders as /private/var/folders once resolved.
        Assert.Contains("No local tools found under ", result.Stdout, StringComparison.Ordinal);
        Assert.Contains(Path.Combine("proj", "effortless-tools") + ".", result.Stdout, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "local-tool-not-inherited-by-child-project: a nested project does not see its parent's effortless-tools/")]
    public async Task ChildProjectDoesNotInheritParentTools()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = SeedProject(cli, server);
        sandbox.WriteFile(
            "child/effortless.json",
            """
            {
              "Name": "child",
              "ProjectSettings": [],
              "ProjectTranspilers": [
                {
                  "Name": "echo-params",
                  "RelativePath": "",
                  "CommandLine": "echo-params -input README.md -output echo.txt"
                }
              ]
            }
            """);
        sandbox.WriteFile("child/README.md", "child");
        var childPath = Path.Combine(sandbox.ProjectPath, "child");

        var build = await cli.Run(["build"], childPath, sandbox);

        Assert.True(build.Failed);
        Assert.DoesNotContain("[local]", build.Stdout, StringComparison.Ordinal);
        Assert.Contains("Project tool 'echo-params' is missing from the current remote tools index", build.Stdout, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(childPath, "echo.txt")));
        Assert.Empty(server.Requests);
    }

    private static Sandbox SeedProject(CliUnderTest cli, MockToolServer server)
    {
        var sandbox = Sandbox.Create(cli);
        sandbox.SeedHome(IndexFixture.Load(server));
        sandbox.SeedProject(Fixture);
        return sandbox;
    }

    private static void AddStep(Sandbox sandbox, string commandLine)
    {
        var project = sandbox.ProjectFile.AsObject();
        project["ProjectTranspilers"] = new JsonArray(
            new JsonObject
            {
                ["Name"] = commandLine.Split(' ')[0],
                ["RelativePath"] = "",
                ["CommandLine"] = commandLine,
            });
        sandbox.WriteFile(
            "effortless.json",
            project.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task<bool> IsListening(int port)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = new CancellationTokenSource(1000);
            await client.ConnectAsync(System.Net.IPAddress.Loopback, port, timeout.Token);
            return true;
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            return false;
        }
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }
}
