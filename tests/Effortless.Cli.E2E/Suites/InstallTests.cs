using System.Text.Json.Nodes;
using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

public sealed class InstallTests
{
    [Fact(DisplayName = "inst-basic: install registers and runs a tool")]
    public async Task InstallRegistersAndRunsTool()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);
        sandbox.WriteFile("in.txt", "hello");
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(
                FileSetEntry.TextFile("out.txt", "HELLO", alwaysOverwrite: true)));

        var result = await cli.Run(
            ["-install", "to-uppercase", "-i", "in.txt", "-o", "out.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("HELLO", sandbox.ReadFile("out.txt"));
        Assert.Contains("[cli] Installed Transpiler", result.Stdout, StringComparison.Ordinal);
        Assert.Contains(
            "COMMAND LINE: to-uppercase -i in.txt -o out.txt",
            result.Stdout,
            StringComparison.Ordinal);
        var step = WorkflowTestSupport.SingleStep(sandbox);
        Assert.Equal("to-uppercase", WorkflowTestSupport.RequiredString(step, "Name"));
        Assert.Equal("", WorkflowTestSupport.RequiredString(step, "RelativePath"));
        Assert.Equal(
            "to-uppercase -i in.txt -o out.txt",
            WorkflowTestSupport.RequiredString(step, "CommandLine"));
        Assert.False(step["IsDisabled"]!.GetValue<bool>());
        Assert.Equal(WorkflowTestSupport.HeadVersion, step["PinnedVersion"]!.GetValue<string>());
        Assert.Null(step["LastVersionUsed"]);
        Assert.Null(step["LastUrl"]);
        Assert.Equal(
            WorkflowTestSupport.SanitizeUrl(server.ToolUri("to-uppercase").ToString()),
            Path.GetFileNameWithoutExtension(
                Assert.Single(WorkflowTestSupport.ZfsFiles(sandbox))));
        server.ThrowIfFaulted();
    }

    [Fact(DisplayName = "inst-bareword: bareword install records the tool command")]
    public async Task BarewordInstallRecordsToolCommand()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);
        sandbox.WriteFile("in.txt", "hello");
        server.Enqueue("to-uppercase", ToolBehavior.Files());

        var result = await cli.Run(
            ["install", "to-uppercase", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            "to-uppercase -i in.txt",
            WorkflowTestSupport.RequiredString(
                WorkflowTestSupport.SingleStep(sandbox),
                "CommandLine"));
        Assert.Single(server.Requests);
    }

    [Fact(DisplayName = "inst-account-prefix: an acct/tool user-set override injects the account")]
    public async Task AccountToolUserSetOverrideInjectsAccount()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = Sandbox.Create(cli);
        sandbox.SeedEmptyHome();
        ToolUrlTestSupport.WriteMinimalProject(sandbox);
        ToolUrlTestSupport.WriteToolUrls(
            sandbox,
            new Dictionary<string, string>
            {
                ["acme/echo"] = server.ToolUri("echo").ToString(),
            });
        sandbox.WriteFile("in.txt", "hello");
        server.Enqueue("echo", ToolBehavior.Files());

        var result = await cli.Run(
            ["install", "acme/echo", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("acme/echo [user-set]", result.Stdout, StringComparison.Ordinal);
        var request = Assert.Single(server.Requests);
        Assert.Equal("acme", request.CliAccount);
        server.ThrowIfFaulted();
    }

    [Fact(DisplayName = "inst-prefix-strip-aliases: every executable alias records the same command")]
    public async Task EveryAliasRecordsSameCommand()
    {
        var observed = new List<string>();
        foreach (var alias in new[] { "dll", "effortless", "ssotme", "aicapture", "aic" })
        {
            var cli = new CliUnderTest();
            await using var server = new MockToolServer();
            using var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);
            sandbox.WriteFile("in.txt", "hello");
            server.Enqueue("to-uppercase", ToolBehavior.Files());
            var args = new[] { "install", "to-uppercase", "-i", "in.txt" };

            var result = alias == "dll"
                ? await cli.Run(args, sandbox.ProjectPath, sandbox)
                : await WorkflowTestSupport.RunAlias(
                    cli,
                    sandbox,
                    alias,
                    args,
                    sandbox.ProjectPath);

            Assert.Equal(0, result.ExitCode);
            observed.Add(
                WorkflowTestSupport.RequiredString(
                    WorkflowTestSupport.SingleStep(sandbox),
                    "CommandLine"));
            Assert.Single(server.Requests);
        }

        Assert.All(observed, command => Assert.Equal("to-uppercase -i in.txt", command));
    }

    [Fact(DisplayName = "inst-subdir: install records and writes beneath the current subdirectory")]
    public async Task InstallRecordsRelativeSubdirectory()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);
        sandbox.WriteFile("in.txt", "subdir input");
        var gen = Directory.CreateDirectory(Path.Combine(sandbox.ProjectPath, "gen")).FullName;
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(
                FileSetEntry.TextFile("out.txt", "SUBDIR INPUT", alwaysOverwrite: true)));

        var result = await cli.Run(
            ["install", "to-uppercase", "-i", "../in.txt"],
            gen,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            "/gen",
            WorkflowTestSupport.RequiredString(
                WorkflowTestSupport.SingleStep(sandbox),
                "RelativePath"));
        Assert.Equal("SUBDIR INPUT", File.ReadAllText(Path.Combine(gen, "out.txt")));
        Assert.Single(WorkflowTestSupport.ZfsFiles(sandbox, "gen"));
    }

    [Fact(DisplayName = "inst-replace: reinstall replaces the existing entry in place")]
    public async Task ReinstallReplacesExistingEntry()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);
        sandbox.WriteFile("in.txt", "hello");
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(),
            ToolBehavior.Files());

        var first = await cli.Run(
            ["install", "to-uppercase", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);
        var originalId = WorkflowTestSupport.SingleStep(sandbox)["ProjectTranspilerId"]?.GetValue<string>();
        var second = await cli.Run(
            ["install", "to-uppercase", "-i", "in.txt", "-p", "x=1"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.Contains("[cli] Replaced Transpiler", second.Stdout, StringComparison.Ordinal);
        var step = WorkflowTestSupport.SingleStep(sandbox);
        Assert.Equal(
            "to-uppercase -i in.txt -p x=1",
            WorkflowTestSupport.RequiredString(step, "CommandLine"));
        Assert.Equal(originalId, step["ProjectTranspilerId"]?.GetValue<string>());
        Assert.Equal(2, server.Requests.Count);
    }

    [Fact(DisplayName = "inst-group: transpiler groups distinguish otherwise matching installs")]
    public async Task TranspilerGroupsDistinguishInstalls()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);
        sandbox.WriteFile("in.txt", "hello");
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(),
            ToolBehavior.Files());

        var docs = await cli.Run(
            ["install", "to-uppercase", "-i", "in.txt", "-tg", "docs"],
            sandbox.ProjectPath,
            sandbox);
        var other = await cli.Run(
            ["install", "to-uppercase", "-i", "in.txt", "-tg", "other"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, docs.ExitCode);
        Assert.Equal(0, other.ExitCode);
        var steps = WorkflowTestSupport.Steps(sandbox)
            .Select(node => Assert.IsType<JsonObject>(node))
            .ToArray();
        Assert.Equal(2, steps.Length);
        Assert.Equal(
            ["docs", "other"],
            steps.Select(step => step["TranspilerGroup"]!.GetValue<string>()).ToArray());
    }

    [Fact(DisplayName = "inst-dry-run: dry-run executes the tool without saving the install")]
    public async Task DryRunExecutesWithoutSaving()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);
        sandbox.WriteFile("in.txt", "hello");
        var before = sandbox.ProjectFile.ToJsonString();
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(
                FileSetEntry.TextFile("dry.txt", "written", alwaysOverwrite: true)));

        var result = await cli.Run(
            ["-install", "to-uppercase", "-i", "in.txt", "-dryRun"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("DRY RUN: Installing", result.Stdout, StringComparison.Ordinal);
        Assert.Equal("written", sandbox.ReadFile("dry.txt"));
        Assert.Equal("workflow-project", sandbox.ProjectFile["Name"]!.GetValue<string>());
        Assert.Empty(WorkflowTestSupport.Steps(sandbox));
        Assert.NotEqual(
            before,
            sandbox.ProjectFile.ToJsonString());
        Assert.Single(server.Requests);
    }

    [Fact(DisplayName = "inst-url: installing a URL registers it without posting")]
    public async Task InstallingUrlDoesNotRunIt()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);
        sandbox.WriteFile("in.txt", "hello");
        var url = new Uri(server.BaseUri, "tools/echo").ToString().TrimEnd('/');

        var result = await cli.Run(
            ["install", url, "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(server.Requests);
        var step = WorkflowTestSupport.SingleStep(sandbox);
        Assert.StartsWith(url, WorkflowTestSupport.RequiredString(step, "CommandLine"), StringComparison.Ordinal);
        Assert.Equal(
            WorkflowTestSupport.SanitizeUrl(url),
            WorkflowTestSupport.RequiredString(step, "Name"));
    }

    [Fact(DisplayName = "inst-target-url: install with -g runs and records the target URL")]
    public async Task InstallWithTargetUrlRunsAndRecordsUrl()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);
        sandbox.WriteFile("in.txt", "hello");
        var url = server.ToolUri("to-uppercase").ToString();
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(
                FileSetEntry.TextFile("target.txt", "TARGET", alwaysOverwrite: true)));

        var result = await cli.Run(
            ["install", "to-uppercase", "-g", url, "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("TARGET", sandbox.ReadFile("target.txt"));
        Assert.Contains(
            $"-g {url}",
            WorkflowTestSupport.RequiredString(
                WorkflowTestSupport.SingleStep(sandbox),
                "CommandLine"),
            StringComparison.Ordinal);
        Assert.Single(server.Requests);
        Assert.Contains(
            WorkflowTestSupport.SanitizeUrl(url),
            Path.GetFileNameWithoutExtension(Assert.Single(WorkflowTestSupport.ZfsFiles(sandbox))),
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "inst-execute: local command install pins legacy command capture")]
    public async Task LocalCommandInstallPinsLegacyCommandCapture()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);
        sandbox.WriteFile(
            "touch.js",
            "require('fs').appendFileSync('marker.txt', 'run\\n');\n");

        var install = await cli.Run(
            ["install", "-execute", "node touch.js"],
            sandbox.ProjectPath,
            sandbox);
        var build = await cli.Run(["build"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, install.ExitCode);
        Assert.True(build.Failed);
        Assert.Contains(
            "Missing closing quote for quoted value",
            build.Combined,
            StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, "marker.txt")));
        var step = WorkflowTestSupport.SingleStep(sandbox);
        Assert.Equal(
            "-execute \"node touch.js",
            WorkflowTestSupport.RequiredString(step, "CommandLine"));
        Assert.Equal("-execute", WorkflowTestSupport.RequiredString(step, "Name"));
        Assert.True(File.Exists(Path.Combine(sandbox.ProjectPath, "errors.json")));
        Assert.Empty(server.Requests);
    }

    [Fact(DisplayName = "inst-execute-timeout: a slow local command is killed and reported as timed out")]
    [Trait("Slow", "true")]
    public async Task SlowLocalCommandIsKilledAndReportedAsTimedOut()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);
        sandbox.WriteFile(
            "sleep.js",
            "setTimeout(() => {}, parseInt(process.argv[2], 10));\n");

        var result = await cli.Run(
            ["-execute", "node sleep.js 5000", "-w", "500"],
            sandbox.ProjectPath,
            sandbox,
            timeoutMs: 20_000);

        Assert.True(result.Failed);
        Assert.Contains(
            "Timed out waiting for process to complete",
            result.Combined,
            StringComparison.Ordinal);
        Assert.Empty(server.Requests);
    }

    [Fact(DisplayName = "inst-execute-missing: a missing local executable fails explicitly")]
    public async Task MissingLocalExecutableFailsExplicitly()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);

        var result = await cli.Run(
            ["-execute", "nosuchcmd"],
            sandbox.ProjectPath,
            sandbox);

        Assert.True(result.Failed);
        Assert.Contains(
            "The system couldn't find the command \"nosuchcmd\"",
            result.Combined,
            StringComparison.Ordinal);
        Assert.Empty(server.Requests);
    }

    [Fact(DisplayName = "inst-auto-build: missing install input triggers the existing build first")]
    public async Task MissingInstallInputTriggersBuild()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("Generator", "/gen", "echo"));
        var gen = Directory.CreateDirectory(Path.Combine(sandbox.ProjectPath, "gen")).FullName;
        server.Enqueue(
            "echo",
            ToolBehavior.Files(
                FileSetEntry.TextFile("a.txt", "generated input", alwaysOverwrite: true)));
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(
                FileSetEntry.TextFile("installed.txt", "GENERATED INPUT", alwaysOverwrite: true)));

        var result = await cli.Run(
            ["install", "to-uppercase", "-i", "a.txt"],
            gen,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Auto-building project before install", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Auto-build completed successfully.", result.Stdout, StringComparison.Ordinal);
        Assert.Equal("generated input", File.ReadAllText(Path.Combine(gen, "a.txt")));
        Assert.Equal("GENERATED INPUT", File.ReadAllText(Path.Combine(gen, "installed.txt")));
        Assert.Equal(["echo", "to-uppercase"], server.Requests.Select(request => request.ToolName).ToArray());
    }

    [Theory(DisplayName = "uninst-basic: uninstall removes an installed step")]
    [InlineData("uninstall")]
    [InlineData("-uninstall")]
    public async Task UninstallRemovesStep(string form)
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("to-uppercase", "", "to-uppercase -i in.txt"));

        var result = await cli.Run(
            [form, "to-uppercase"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[cli] Uninstalled Transpiler", result.Stdout, StringComparison.Ordinal);
        Assert.Empty(WorkflowTestSupport.Steps(sandbox));
    }

    [Fact(DisplayName = "uninst-prompt-no: declining cleanup keeps generated files")]
    public async Task DecliningUninstallCleanupKeepsGeneratedFiles()
    {
        var (result, sandbox, server) = await RunInstalledUninstall("n\n");
        await using (server)
        using (sandbox)
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                "Would you like to delete all files generated by this tool? (y/n):",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(sandbox.ProjectPath, "generated.txt")));
            Assert.Empty(WorkflowTestSupport.Steps(sandbox));
        }
    }

    [Fact(DisplayName = "uninst-prompt-yes: accepting cleanup removes generated files")]
    public async Task AcceptingUninstallCleanupRemovesGeneratedFiles()
    {
        var (result, sandbox, server) = await RunInstalledUninstall("y\n");
        await using (server)
        using (sandbox)
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("[cli] Cleaned generated files.", result.Stdout, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, "generated.txt")));
            Assert.Empty(WorkflowTestSupport.Steps(sandbox));
        }
    }

    [Fact(DisplayName = "uninst-qualified: short names match fully-qualified installs")]
    public async Task ShortNameMatchesQualifiedInstall()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep(
                "to-uppercase",
                "",
                "effortless/common/to-uppercase -i in.txt"));

        var result = await cli.Run(
            ["uninstall", "to-uppercase"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(WorkflowTestSupport.Steps(sandbox));
    }

    [Fact(DisplayName = "uninst-missing-name: uninstall requires a tool name")]
    public async Task UninstallRequiresName()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);

        var result = await cli.Run(["uninstall"], sandbox.ProjectPath, sandbox);

        Assert.True(result.Failed);
        Assert.Contains(
            "Please specify a transpiler name to uninstall",
            result.Combined,
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "uninst-none: uninstall with no match is a successful no-op")]
    public async Task UninstallNoMatchIsSuccessfulNoOp()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);

        var result = await cli.Run(
            ["uninstall", "nope"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "[cli] No tools matching tool name 'nope' in path ''",
            result.Stdout,
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "uninst-ambiguous: group disambiguates multiple matching installs")]
    public async Task GroupDisambiguatesMultipleMatches()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep(
                "to-uppercase",
                "",
                "to-uppercase -i a.txt",
                TranspilerGroup: "a"),
            new WorkflowStep(
                "to-uppercase",
                "",
                "to-uppercase -i b.txt",
                TranspilerGroup: "b"));

        var ambiguous = await cli.Run(
            ["uninstall", "to-uppercase"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, ambiguous.ExitCode);
        Assert.Contains("matched multiple tools", ambiguous.Stdout, StringComparison.Ordinal);
        Assert.Contains("to-uppercase -i a.txt", ambiguous.Stdout, StringComparison.Ordinal);
        Assert.Contains("to-uppercase -i b.txt", ambiguous.Stdout, StringComparison.Ordinal);
        Assert.Equal(2, WorkflowTestSupport.Steps(sandbox).Count);

        var selected = await cli.Run(
            ["uninstall", "to-uppercase", "-tg", "a"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, selected.ExitCode);
        var remaining = WorkflowTestSupport.SingleStep(sandbox);
        Assert.Equal("b", remaining["TranspilerGroup"]!.GetValue<string>());
    }

    private static async Task<(CliResult Result, Sandbox Sandbox, MockToolServer Server)>
        RunInstalledUninstall(string stdin)
    {
        var cli = new CliUnderTest();
        var server = new MockToolServer();
        var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);
        sandbox.WriteFile("in.txt", "hello");
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(
                FileSetEntry.TextFile("generated.txt", "generated", alwaysOverwrite: true)));
        var install = await cli.Run(
            ["install", "to-uppercase", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);
        Assert.Equal(0, install.ExitCode);
        Assert.True(File.Exists(Path.Combine(sandbox.ProjectPath, "generated.txt")));
        var ledger = Assert.Single(WorkflowTestSupport.ZfsFiles(sandbox));
        var expectedLedger = Path.Combine(
            WorkflowTestSupport.ZfsDirectory(sandbox),
            "to-uppercase.zfs");
        // The prompt branch probes the name-derived key; normal HTTP installs write a URL-derived key.
        // Seed the exact "installed step with .zfs" state required by the two prompt cases.
        File.Move(ledger, expectedLedger);
        Assert.True(File.Exists(expectedLedger));

        var uninstall = await cli.Run(
            ["uninstall", "to-uppercase"],
            sandbox.ProjectPath,
            sandbox,
            stdin);
        return (uninstall, sandbox, server);
    }
}
