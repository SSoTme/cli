using System.Text.Json;
using System.Text.Json.Nodes;
using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

public sealed class BuildTests
{
    [Theory(DisplayName = "build-basic: build forms run enabled steps in file order")]
    [InlineData("build")]
    [InlineData("-build")]
    [InlineData("-b")]
    public async Task BuildRunsEnabledStepsInOrder(string form)
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("Root", "", "to-uppercase"),
            new WorkflowStep("Sub", "/sub", "echo"));
        Directory.CreateDirectory(Path.Combine(sandbox.ProjectPath, "sub"));
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(
                FileSetEntry.TextFile("root.txt", "root", alwaysOverwrite: true)));
        server.Enqueue(
            "echo",
            ToolBehavior.Files(
                FileSetEntry.TextFile("sub.txt", "sub", alwaysOverwrite: true)));

        var result = await cli.Run([form], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("root", sandbox.ReadFile("root.txt"));
        Assert.Equal("sub", sandbox.ReadFile("sub/sub.txt"));
        Assert.True(
            result.Stdout.IndexOf("**** : Root ****", StringComparison.Ordinal)
            < result.Stdout.IndexOf("**** /sub: Sub ****", StringComparison.Ordinal));
        Assert.Equal(["to-uppercase", "echo"], server.Requests.Select(request => request.ToolName).ToArray());
        Golden.AssertMatches(
            Behavior.IsLegacy
                ? "build-basic"
                : "build-basic-rebuild",
            Golden.Normalize(result.Stdout, sandbox, server.BaseUri.ToString()));
        var steps = WorkflowTestSupport.Steps(sandbox)
            .Select(node => Assert.IsType<JsonObject>(node))
            .ToArray();
        Assert.All(
            steps,
            step =>
            {
                Assert.Equal(
                    WorkflowTestSupport.HeadVersion,
                    step["LastVersionUsed"]!.GetValue<string>());
                Assert.StartsWith(
                    server.BaseUri.ToString(),
                    step["LastUrl"]!.GetValue<string>(),
                    StringComparison.Ordinal);
            });
    }

    [Fact(DisplayName = "build-cwd-per-step: a step executes in its RelativePath")]
    public async Task StepExecutesInRelativePath()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("Sub", "/sub", "echo -i ../in.txt"));
        sandbox.WriteFile("in.txt", "relative input");
        Directory.CreateDirectory(Path.Combine(sandbox.ProjectPath, "sub"));
        server.Enqueue(
            "echo",
            ToolBehavior.Files(
                FileSetEntry.TextFile("out.txt", "relative output", alwaysOverwrite: true)));

        var result = await cli.Run(["build"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("relative output", sandbox.ReadFile("sub/out.txt"));
        var request = Assert.Single(server.Requests);
        Assert.Equal(["../in.txt"], request.CliInput);
        var input = Assert.Single(request.InputFiles);
        Assert.Equal("in.txt", input.RelativePath);
        Assert.Equal("relative input", input.Text);
    }

    [Fact(DisplayName = "build-skips-disabled: includeDisabled controls disabled steps")]
    public async Task IncludeDisabledControlsDisabledSteps()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("Disabled", "", "to-uppercase", IsDisabled: true));

        var skipped = await cli.Run(["build"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, skipped.ExitCode);
        Assert.Contains("SKIPPING DISABLED TRANSPILER", skipped.Stdout, StringComparison.Ordinal);
        Assert.Empty(server.Requests);

        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(
                FileSetEntry.TextFile("included.txt", "included", alwaysOverwrite: true)));
        var included = await cli.Run(
            ["build", "-id"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, included.ExitCode);
        Assert.Equal("included", sandbox.ReadFile("included.txt"));
        Assert.Single(server.Requests);
    }

    [Fact(DisplayName = "build-group: build selects a group case-insensitively")]
    public async Task BuildSelectsGroupCaseInsensitively()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("A", "", "to-uppercase", TranspilerGroup: "a"),
            new WorkflowStep("B", "", "echo", TranspilerGroup: "b"));
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(
                FileSetEntry.TextFile("a.txt", "a", alwaysOverwrite: true)));

        var result = await cli.Run(
            ["build", "-tg", "A"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("a", sandbox.ReadFile("a.txt"));
        Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, "b.txt")));
        Assert.Equal("to-uppercase", Assert.Single(server.Requests).ToolName);
    }

    [Fact(DisplayName = "build-subtree: build from a subdirectory runs only its subtree")]
    public async Task BuildFromSubdirectoryRunsOnlySubtree()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("Root", "", "to-uppercase"),
            new WorkflowStep("Sub", "/sub", "echo"),
            new WorkflowStep("Deep", "/sub/deep", "to-uppercase"));
        var sub = Directory.CreateDirectory(Path.Combine(sandbox.ProjectPath, "sub")).FullName;
        Directory.CreateDirectory(Path.Combine(sub, "deep"));
        server.Enqueue(
            "echo",
            ToolBehavior.Files(
                FileSetEntry.TextFile("sub.txt", "sub", alwaysOverwrite: true)));
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(
                FileSetEntry.TextFile("deep.txt", "deep", alwaysOverwrite: true)));

        var result = await cli.Run(["build"], sub, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, "root.txt")));
        Assert.Equal("sub", sandbox.ReadFile("sub/sub.txt"));
        Assert.Equal("deep", sandbox.ReadFile("sub/deep/deep.txt"));
        Assert.Equal(["echo", "to-uppercase"], server.Requests.Select(request => request.ToolName).ToArray());
    }

    [Theory(DisplayName = "build-local: buildLocal forms run only the exact current path")]
    [InlineData("-buildLocal")]
    [InlineData("buildlocal")]
    [InlineData("-bl")]
    public async Task BuildLocalRunsOnlyExactPath(string form)
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("Root", "", "to-uppercase"),
            new WorkflowStep("Sub", "/sub", "echo"),
            new WorkflowStep("Deep", "/sub/deep", "to-uppercase"));
        var sub = Directory.CreateDirectory(Path.Combine(sandbox.ProjectPath, "sub")).FullName;
        Directory.CreateDirectory(Path.Combine(sub, "deep"));
        server.Enqueue(
            "echo",
            ToolBehavior.Files(
                FileSetEntry.TextFile("local.txt", "local", alwaysOverwrite: true)));

        var result = await cli.Run([form], sub, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("local", sandbox.ReadFile("sub/local.txt"));
        Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, "deep.txt")));
        Assert.Equal("echo", Assert.Single(server.Requests).ToolName);
    }

    [Fact(DisplayName = "build-all: buildAll runs the whole project from the root")]
    public async Task BuildAllRunsTheWholeProjectFromTheRoot()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("Root", "", "to-uppercase"),
            new WorkflowStep("Sub", "/sub", "echo"));
        var sub = Directory.CreateDirectory(
            Path.Combine(sandbox.ProjectPath, "sub")).FullName;
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(
                FileSetEntry.TextFile("root.txt", "root", alwaysOverwrite: true)));
        server.Enqueue(
            "echo",
            ToolBehavior.Files(
                FileSetEntry.TextFile("sub.txt", "sub", alwaysOverwrite: true)));

        // Run from /sub: buildAll behaves as if it were run from the root.
        var result = await cli.Run(["buildAll"], sub, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("root", sandbox.ReadFile("root.txt"));
        Assert.Equal("sub", sandbox.ReadFile("sub/sub.txt"));
        Assert.Equal(
            ["to-uppercase", "echo"],
            server.Requests.Select(request => request.ToolName).ToArray());
    }

    [Fact(DisplayName = "build-all-excludes-nested: buildAll stops at nested project boundaries")]
    public async Task BuildAllDoesNotEnterNestedProjects()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("Root", "", "to-uppercase"));
        var nested = Path.Combine(sandbox.ProjectPath, "nested");
        WorkflowTestSupport.WriteProjectAt(
            nested,
            new WorkflowStep("Nested", "", "echo"));
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(
                FileSetEntry.TextFile("root.txt", "root", alwaysOverwrite: true)));

        var result = await cli.Run(["buildAll"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("root", sandbox.ReadFile("root.txt"));

        // D6/D12: nested effortless projects are normally excluded. Only the
        // root step ran, and no child process was spawned for the nested one.
        Assert.False(File.Exists(Path.Combine(nested, "nested.txt")));
        Assert.DoesNotContain(
            "Executing 'effortless -buildLocal' in ",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Equal(
            ["to-uppercase"],
            server.Requests.Select(request => request.ToolName).ToArray());
    }

    [Theory(DisplayName = "build-with-subprojects-includes-nested: buildWithSubprojects builds nested projects")]
    [Trait("Slow", "true")]
    [InlineData("buildWithSubprojects")]
    [InlineData("-buildWithSubprojects")]
    [InlineData("-bws")]
    public async Task BuildWithSubprojectsRunsNestedProjects(string form)
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("Root", "", "to-uppercase"));
        var nested = Path.Combine(sandbox.ProjectPath, "nested");
        WorkflowTestSupport.WriteProjectAt(
            nested,
            new WorkflowStep("Nested", "", "echo"));
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(
                FileSetEntry.TextFile("root.txt", "root", alwaysOverwrite: true)));
        server.Enqueue(
            "echo",
            ToolBehavior.Files(
                FileSetEntry.TextFile("nested.txt", "nested", alwaysOverwrite: true)));

        var result = await cli.Run(
            [form],
            sandbox.ProjectPath,
            sandbox,
            timeoutMs: 180_000);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("root", sandbox.ReadFile("root.txt"));
        Assert.Equal("nested", sandbox.ReadFile("nested/nested.txt"));
        Assert.Contains(
            "Executing 'effortless -buildLocal' in ",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Contains("/nested", result.Stdout, StringComparison.Ordinal);
        Assert.Equal(
            ["to-uppercase", "echo"],
            server.Requests.Select(request => request.ToolName).ToArray());
    }

    [Fact(DisplayName = "build-fail-stops: a failed step stops the default build")]
    public async Task FailedStepStopsDefaultBuild()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("A", "", "to-uppercase"),
            new WorkflowStep("B", "", "echo"));
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files().WithLogs(new MockLog("error", "A exploded")));

        var result = await cli.Run(["build"], sandbox.ProjectPath, sandbox);

        Assert.True(result.Failed);
        Assert.Contains("Transpiler 'A' failed: A exploded", result.Combined, StringComparison.Ordinal);
        Assert.Contains("BUILD FAILED", result.Combined, StringComparison.Ordinal);
        Assert.Equal("to-uppercase", Assert.Single(server.Requests).ToolName);
        using var errors = WorkflowTestSupport.ReadJsonDocument(
            Path.Combine(sandbox.ProjectPath, "errors.json"));
        var root = errors.RootElement;
        Assert.Equal(["A"], root.GetProperty("failedStepNames").EnumerateArray().Select(x => x.GetString()).ToArray());
        var error = Assert.Single(root.GetProperty("errors").EnumerateArray().ToArray());
        Assert.Equal("failed", error.GetProperty("status").GetString());
        Assert.Equal("A exploded", error.GetProperty("transpilerException").GetProperty("message").GetString());
    }

    [Fact(DisplayName = "build-coe-continues: continue-on-error aliases run later steps and succeed")]
    public async Task ContinueOnErrorAliasesRunLaterSteps()
    {
        foreach (var option in new[] { "-continueOnError", "-coe", "-ignoreErrors" })
        {
            var cli = new CliUnderTest();
            await using var server = new MockToolServer();
            using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
                cli,
                server,
                new WorkflowStep("A", "", "to-uppercase"),
                new WorkflowStep("B", "", "echo"));
            server.Enqueue(
                "to-uppercase",
                ToolBehavior.Files().WithLogs(new MockLog("error", "A exploded")));
            server.Enqueue(
                "echo",
                ToolBehavior.Files(
                    FileSetEntry.TextFile("b.txt", "usable", alwaysOverwrite: true)));

            var result = await cli.Run(
                ["build", option],
                sandbox.ProjectPath,
                sandbox);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("usable", sandbox.ReadFile("b.txt"));
            Assert.Contains(
                "-continueOnError is set — moving on",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.Contains(
                "BUILD FINISHED WITH 1 FAILED STEP(S) — 1 step(s) succeeded and are usable.",
                result.Stdout,
                StringComparison.Ordinal);
            using var errors = WorkflowTestSupport.ReadJsonDocument(
                Path.Combine(sandbox.ProjectPath, "errors.json"));
            Assert.True(errors.RootElement.GetProperty("continueOnError").GetBoolean());
            Assert.Equal(1, errors.RootElement.GetProperty("succeededSteps").GetInt32());
            Assert.Equal(2, server.Requests.Count);
        }
    }

    [Fact(DisplayName = "build-coe-throwing-step: continue-on-error records CLI-side payload failures")]
    public async Task ContinueOnErrorRecordsCliSideThrow()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("BrokenJson", "", "to-uppercase"));
        server.Enqueue("to-uppercase", ToolBehavior.Text("not json"));

        var result = await cli.Run(
            ["build", "-coe"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        using var errors = WorkflowTestSupport.ReadJsonDocument(
            Path.Combine(sandbox.ProjectPath, "errors.json"));
        var error = Assert.Single(
            errors.RootElement.GetProperty("errors").EnumerateArray().ToArray());
        Assert.Equal(-1, error.GetProperty("exitCode").GetInt32());
        Assert.True(
            error.TryGetProperty("cliException", out _)
            || error.TryGetProperty("transpilerException", out _),
            "The malformed payload must retain an exception record.");
    }

    [Fact(DisplayName = "build-errors-json-cleared: a clean build removes stale errors.json")]
    public async Task CleanBuildRemovesStaleErrorsFile()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("Good", "", "to-uppercase"));
        sandbox.WriteFile("errors.json", """{"schema":"stale"}""");
        server.Enqueue("to-uppercase", ToolBehavior.Files());

        var result = await cli.Run(["build"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "[cli] build clean — removed stale errors.json",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, "errors.json")));
    }

    [Fact(DisplayName = "build-errors-json-schema: failure ledger uses the public v1 schema")]
    public async Task FailureLedgerUsesPublicSchema()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("Fail", "", "to-uppercase"),
            new WorkflowStep("Pass", "", "echo"),
            new WorkflowStep("Skip", "", "to-uppercase", IsDisabled: true));
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files().WithLogs(new MockLog("error", "failed")));
        server.Enqueue("echo", ToolBehavior.Files());

        var result = await cli.Run(
            ["build", "-coe"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        using var document = WorkflowTestSupport.ReadJsonDocument(
            Path.Combine(sandbox.ProjectPath, "errors.json"));
        var root = document.RootElement;
        Assert.Equal("effortless-build-errors/v1", root.GetProperty("schema").GetString());
        Assert.True(DateTimeOffset.TryParse(root.GetProperty("generatedAt").GetString(), out _));
        Assert.True(DateTimeOffset.TryParse(root.GetProperty("startedAt").GetString(), out _));
        Assert.Equal("build", root.GetProperty("buildCommand").GetString());
        Assert.Equal(3, root.GetProperty("totalSteps").GetInt32());
        Assert.Equal(["Fail"], root.GetProperty("failedStepNames").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal(
            ["failed", "succeeded", "skipped"],
            root.GetProperty("steps").EnumerateArray()
                .Select(step => step.GetProperty("status").GetString())
                .ToArray());
        var error = Assert.Single(root.GetProperty("errors").EnumerateArray().ToArray());
        Assert.Equal(WorkflowTestSupport.HeadVersion, error.GetProperty("resolvedVersion").GetString());
        Assert.StartsWith(
            server.BaseUri.ToString(),
            error.GetProperty("resolvedUrl").GetString(),
            StringComparison.Ordinal);
        Assert.All(
            root.EnumerateObject(),
            property => Assert.True(char.IsLower(property.Name[0]), $"'{property.Name}' is not camelCase."));
        Golden.AssertMatches(
            "build-errors-json-schema",
            Golden.Normalize(
                File.ReadAllText(Path.Combine(sandbox.ProjectPath, "errors.json")),
                sandbox,
                server.BaseUri.ToString()));
    }

    [Fact(DisplayName = "build-version-label: automatic freshness labels all project tools at HEAD")]
    public async Task BuildLabelsAutomaticallyUpgradedToolsAsLatest()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep(
                "Pinned",
                "",
                "to-uppercase",
                PinnedVersion: WorkflowTestSupport.OldVersion),
            new WorkflowStep("Latest", "", "echo"));
        server.Enqueue("to-uppercase", ToolBehavior.Files());
        server.Enqueue("echo", ToolBehavior.Files());

        var result = await cli.Run(["build"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);

        // D17: a pin the catalog can still satisfy is honored by the automatic
        // gate, so the pinned step reports its pinned version, not HEAD. The
        // unpinned step still tracks HEAD.
        Assert.Contains(
            $"cli:> effortless/common/to-uppercase {WorkflowTestSupport.OldVersion} [pinned]",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Contains(
            $"cli:> effortless/common/echo {WorkflowTestSupport.HeadVersion} [latest]",
            result.Stdout,
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "build-pinned-url: automatic freshness honors a resolvable PinnedVersion")]
    public async Task AutomaticFreshnessHonorsAResolvablePin()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep(
                "Pinned",
                "",
                "to-uppercase",
                PinnedVersion: WorkflowTestSupport.OldVersion));
        server.Enqueue("to-uppercase", ToolBehavior.Files());

        var result = await cli.Run(["build"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);

        // D17 supersedes step-03A here: a step is pinned or it follows HEAD.
        // The currentness gate no longer discards a pin the catalog can still
        // satisfy, in either the ported or the legacy binary.
        Assert.Equal(
            WorkflowTestSupport.OldVersion,
            Assert.Single(server.Requests).Version);
        Assert.Equal(
            WorkflowTestSupport.OldVersion,
            WorkflowTestSupport.SingleStep(sandbox)["PinnedVersion"]!.GetValue<string>());
    }

    [Fact(DisplayName = "build-pinned-missing: an unavailable hard pin is cleared before build")]
    public async Task MissingPinnedVersionIsClearedBeforeBuild()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("Missing", "", "to-uppercase", PinnedVersion: "v9"));
        if (!Behavior.IsLegacy)
        {
            server.Enqueue("to-uppercase", ToolBehavior.Files());
        }

        var result = await cli.Run(["build"], sandbox.ProjectPath, sandbox);

        if (Behavior.IsLegacy)
        {
            Assert.True(result.Failed);
            Assert.Contains(
                "Error: version 'v9' not found for tool",
                result.Combined,
                StringComparison.Ordinal);
            Assert.Empty(server.Requests);
        }
        else
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(
                WorkflowTestSupport.HeadVersion,
                Assert.Single(server.Requests).Version);
            Assert.Null(
                WorkflowTestSupport.SingleStep(sandbox)["PinnedVersion"]);
        }
    }

    [Fact(DisplayName = "build-sync-commandline-version: automatic freshness removes embedded versions")]
    public async Task AutomaticFreshnessRemovesEmbeddedCommandVersion()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep(
                "Versioned",
                "",
                $"effortless/common/to-uppercase/{WorkflowTestSupport.OldVersion}"));
        server.Enqueue("to-uppercase", ToolBehavior.Files());

        var result = await cli.Run(["build"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            Behavior.IsLegacy
                ? $"effortless/common/to-uppercase/{WorkflowTestSupport.HeadVersion}"
                : "effortless/common/to-uppercase",
            WorkflowTestSupport.RequiredString(
                WorkflowTestSupport.SingleStep(sandbox),
                "CommandLine"));
        Assert.Equal(WorkflowTestSupport.HeadVersion, Assert.Single(server.Requests).Version);
    }

    [Fact(DisplayName = "build-soft-pin-updates: unpinned builds track moving HEAD without redundant saves")]
    public async Task UnpinnedBuildTracksMovingHead()
    {
        const string movedVersion = "v2026.02.02.0002";
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("Moving", "", "to-uppercase"));
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(),
            ToolBehavior.Files(),
            ToolBehavior.Files());

        var first = await cli.Run(["build"], sandbox.ProjectPath, sandbox);
        Assert.Equal(0, first.ExitCode);
        Assert.Equal(
            WorkflowTestSupport.HeadVersion,
            WorkflowTestSupport.SingleStep(sandbox)["LastVersionUsed"]!.GetValue<string>());

        WorkflowTestSupport.WriteMovingIndex(
            sandbox,
            server,
            movedVersion,
            WorkflowTestSupport.HeadVersion);
        var second = await cli.Run(["build"], sandbox.ProjectPath, sandbox);
        Assert.Equal(0, second.ExitCode);
        var step = WorkflowTestSupport.SingleStep(sandbox);
        Assert.Equal(movedVersion, step["LastVersionUsed"]!.GetValue<string>());
        Assert.Equal(
            server.ToolUri("to-uppercase", movedVersion).ToString(),
            step["LastUrl"]!.GetValue<string>());

        var projectPath = Path.Combine(sandbox.ProjectPath, "effortless.json");
        var savedAt = File.GetLastWriteTimeUtc(projectPath);
        await Task.Delay(1_100);
        var third = await cli.Run(["build"], sandbox.ProjectPath, sandbox);
        Assert.Equal(0, third.ExitCode);
        Assert.Equal(savedAt, File.GetLastWriteTimeUtc(projectPath));
        Assert.Equal(
            [WorkflowTestSupport.HeadVersion, movedVersion, movedVersion],
            server.Requests.Select(request => request.Version).ToArray());
    }

    [Fact(DisplayName = "build-debug: debug propagates to steps and writes wire XML")]
    public async Task DebugPropagatesAndWritesXml()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("Debug", "", "to-uppercase"));
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(
                FileSetEntry.TextFile("debug.txt", "debug", alwaysOverwrite: true)));

        var result = await cli.Run(
            ["build", "-debug"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "CommandLine:> effortless to-uppercase -debug",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Contains(
            $"POST {server.ToolUri("to-uppercase")}",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.True(Assert.Single(server.Requests).CliDebug);
        var xml = Assert.Single(
            Directory.GetFiles(
                Path.Combine(sandbox.ProjectPath, ".ssotme"),
                "*.xml",
                SearchOption.AllDirectories));
        Assert.Contains("<FileSet", File.ReadAllText(xml), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "build-empty-folder-cleanup: cleanup protects cwd and transpiler paths")]
    public async Task EmptyFolderCleanupProtectsImportantDirectories()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep(
                "Protected",
                "/work/sub",
                "to-uppercase",
                IsDisabled: true));
        var work = Directory.CreateDirectory(Path.Combine(sandbox.ProjectPath, "work")).FullName;
        var junk = Directory.CreateDirectory(Path.Combine(work, "junk")).FullName;
        var protectedSub = Directory.CreateDirectory(Path.Combine(work, "sub")).FullName;

        var result = await cli.Run(["build"], work, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.False(Directory.Exists(junk));
        Assert.True(Directory.Exists(protectedSub));
        Assert.True(Directory.Exists(work));
        Assert.True(Directory.Exists(sandbox.ProjectPath));
    }

    [Fact(DisplayName = "build-nested-parent-root: child build runs parent ssot unless local")]
    [Trait("Slow", "true")]
    public async Task ChildBuildRunsParentSsotUnlessLocal()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep(
                "ParentSsot",
                "",
                "to-uppercase",
                TranspilerGroup: "ssot"),
            new WorkflowStep(
                "ParentOther",
                "",
                "echo",
                TranspilerGroup: "other"));
        var child = Path.Combine(sandbox.ProjectPath, "child");
        WorkflowTestSupport.WriteProjectAt(
            child,
            new WorkflowStep("Child", "", "echo"));
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(
                FileSetEntry.TextFile("parent.txt", "parent", alwaysOverwrite: true)));
        server.Enqueue(
            "echo",
            ToolBehavior.Files(
                FileSetEntry.TextFile("child.txt", "child", alwaysOverwrite: true)),
            ToolBehavior.Files(
                FileSetEntry.TextFile("local.txt", "local", alwaysOverwrite: true)));

        var normal = await cli.Run(
            ["build"],
            child,
            sandbox,
            timeoutMs: 180_000);
        Assert.Equal(0, normal.ExitCode);
        Assert.Equal("parent", sandbox.ReadFile("parent.txt"));
        Assert.Equal("child", sandbox.ReadFile("child/child.txt"));

        var local = await cli.Run(
            ["-buildLocal"],
            child,
            sandbox,
            timeoutMs: 180_000);

        Assert.Equal(0, local.ExitCode);
        Assert.Equal("local", sandbox.ReadFile("child/local.txt"));
        Assert.False(File.Exists(Path.Combine(child, "child.txt")));
        Assert.Contains("**** : ParentSsot ****", normal.Stdout, StringComparison.Ordinal);
        Assert.Contains("**** : Child ****", normal.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("ParentOther", normal.Stdout, StringComparison.Ordinal);
        Assert.True(
            normal.Stdout.IndexOf("**** : ParentSsot ****", StringComparison.Ordinal)
            < normal.Stdout.IndexOf("**** : Child ****", StringComparison.Ordinal));
        Assert.Equal(
            ["to-uppercase", "echo", "echo"],
            server.Requests.Select(request => request.ToolName).ToArray());
    }

    [Fact(DisplayName = "build-no-steps: an empty project builds successfully")]
    public async Task EmptyProjectBuildsSuccessfully()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);

        var result = await cli.Run(["build"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, "errors.json")));
        Assert.Empty(server.Requests);
    }

    [Fact(DisplayName = "build-project-required: build outside a project fails")]
    public async Task BuildOutsideProjectFails()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);

        var result = await cli.Run(["build"], sandbox.ProjectPath, sandbox);

        Assert.True(result.Failed);
        Assert.Contains(
            "ERROR: No project found in this directory",
            result.Combined,
            StringComparison.Ordinal);
    }
}
