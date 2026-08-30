using System.Text.Json.Nodes;
using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

public sealed class ResolutionTests
{
    [Fact(DisplayName = "res-head-latest: head version resolves with [latest]")]
    public async Task HeadVersionResolvesAsLatest()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server);
        server.Enqueue("to-uppercase", ToolBehavior.Echo());
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index);
        ResolutionTestSupport.SeedProject(sandbox);

        var result = await cli.Run(
            ["to-uppercase", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        var request = Assert.Single(server.Requests);
        Assert.Equal("to-uppercase", request.ToolName);
        Assert.Equal(ResolutionTestSupport.HeadVersion, request.Version);
        Assert.Contains(
            $"cli:> effortless/common/to-uppercase {ResolutionTestSupport.HeadVersion} [latest]",
            result.Stdout,
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "res-explicit-version: explicit /vX")]
    public async Task ExplicitVersionResolvesRequestedVersionWithoutSuffix()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server);
        server.Enqueue("to-uppercase", ToolBehavior.Echo());
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index);
        ResolutionTestSupport.SeedProject(sandbox);

        var result = await cli.Run(
            [$"to-uppercase/{ResolutionTestSupport.OldVersion}", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        var request = Assert.Single(server.Requests);
        Assert.Equal(ResolutionTestSupport.OldVersion, request.Version);
        Assert.Contains(
            $"cli:> effortless/common/to-uppercase {ResolutionTestSupport.OldVersion}",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"{ResolutionTestSupport.OldVersion} [",
            result.Stdout,
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "res-explicit-missing: explicit version missing")]
    public async Task ExplicitMissingVersionPrintsSpecificErrorOnly()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server);
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index);
        ResolutionTestSupport.SeedProject(sandbox);

        var result = await cli.Run(
            ["to-uppercase/v7", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "Error: version 'v7' not found for tool 'effortless/common/to-uppercase'.",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Contains(ResolutionTestSupport.HeadVersion, result.Stdout, StringComparison.Ordinal);
        Assert.Contains(ResolutionTestSupport.OldVersion, result.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("ERROR: Tool", result.Stdout, StringComparison.Ordinal);
        Assert.Empty(server.Requests);
    }

    [Fact(DisplayName = "res-pinned-labels: pinned labels")]
    public async Task LegacyDirectRunsResolveBeforeLoadingProjectPins()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server);
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Echo(),
            ToolBehavior.Echo());
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index);

        ResolutionTestSupport.SeedProject(
            sandbox,
            new ResolutionProjectStep(
                "To Uppercase",
                "",
                "to-uppercase -i in.txt",
                ResolutionTestSupport.OldVersion));
        var oldPin = await cli.Run(
            ["to-uppercase", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        ResolutionTestSupport.SeedProject(
            sandbox,
            new ResolutionProjectStep(
                "To Uppercase",
                "",
                "to-uppercase -i in.txt",
                ResolutionTestSupport.HeadVersion));
        var headPin = await cli.Run(
            ["to-uppercase", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, oldPin.ExitCode);
        Assert.Equal(0, headPin.ExitCode);
        Assert.Contains(
            $"{ResolutionTestSupport.HeadVersion} [latest]",
            oldPin.Stdout,
            StringComparison.Ordinal);
        Assert.Contains(
            $"{ResolutionTestSupport.HeadVersion} [latest]",
            headPin.Stdout,
            StringComparison.Ordinal);
        Assert.Equal(
            [ResolutionTestSupport.HeadVersion, ResolutionTestSupport.HeadVersion],
            server.Requests.Select(request => request.Version));
    }

    [Fact(DisplayName = "res-latest-flag: -latest runs head and clears the pin")]
    public async Task LatestRunsHeadButLegacyCannotMutateDirectInvocationStep()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server);
        server.Enqueue("to-uppercase", ToolBehavior.Echo());
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index);
        ResolutionTestSupport.SeedProject(
            sandbox,
            new ResolutionProjectStep(
                "To Uppercase",
                "",
                "to-uppercase -i in.txt",
                ResolutionTestSupport.OldVersion));

        var result = await cli.Run(
            ["to-uppercase", "-i", "in.txt", "-latest"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            ResolutionTestSupport.HeadVersion,
            Assert.Single(server.Requests).Version);
        Assert.Contains(
            $"{ResolutionTestSupport.HeadVersion} [latest]",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Cleared hard pin", result.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("Now using", result.Stdout, StringComparison.Ordinal);

        var step = ResolutionTestSupport.FindStep(
            ResolutionTestSupport.ReadProject(sandbox),
            "to-uppercase");
        Assert.Equal(
            ResolutionTestSupport.OldVersion,
            step["PinnedVersion"]?.GetValue<string>());
        Assert.Null(step["LastVersionUsed"]);
    }

    [Fact(DisplayName = "res-no-head: tool without a head version")]
    public async Task NoHeadPrintsSpecificErrorButLegacyContinuesToUserOverride()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server, "resolution-no-head");
        server.Enqueue("no-head-override", ToolBehavior.Echo());
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index);
        ResolutionTestSupport.WriteToolUrls(
            sandbox,
            new Dictionary<string, string>
            {
                ["cli-cloud-bridge"] = index.BridgeUri.ToString(),
                ["to-uppercase"] = server.ToolUri("no-head-override").ToString(),
            });
        ResolutionTestSupport.SeedProject(sandbox);

        var result = await cli.Run(
            ["to-uppercase", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "Error: this tool has no head versions; please specify a version to run via ssotme to-uppercase/version.",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Contains("cli:> to-uppercase [user-set]", result.Stdout, StringComparison.Ordinal);
        Assert.Equal("no-head-override", Assert.Single(server.Requests).ToolName);
    }

    [Fact(DisplayName = "res-ambiguous: ambiguous short name prefers effortless/")]
    public async Task AmbiguousShortNamePrefersEffortlessAccount()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server, "resolution-ambiguous");
        server.Enqueue("effortless-echo", ToolBehavior.Echo());
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index);
        ResolutionTestSupport.SeedProject(sandbox);

        var result = await cli.Run(["echo", "-i", "in.txt"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "Warning: 'echo' matched multiple tools: acme/x/echo, effortless/y/echo. Using 'effortless/y/echo'.",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Equal("effortless-echo", Assert.Single(server.Requests).ToolName);
    }

    [Fact(DisplayName = "res-qualified: qualified names")]
    public async Task QualifiedNameSelectsExactToolWithoutWarning()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server, "resolution-ambiguous");
        server.Enqueue("acme-echo", ToolBehavior.Echo());
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index);
        ResolutionTestSupport.SeedProject(sandbox);

        var result = await cli.Run(
            ["acme/x/echo", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("matched multiple tools", result.Stdout, StringComparison.Ordinal);
        Assert.Equal("acme-echo", Assert.Single(server.Requests).ToolName);
    }

    [Fact(DisplayName = "res-refresh-on-miss: a missing tool triggers one refresh")]
    public async Task MissingToolInNonEmptyLegacyCacheDoesNotTriggerRefresh()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        var staleIndex = IndexFixture.Load(toolServer);
        var refreshedIndex = IndexFixture.Load(toolServer, "resolution-refreshed");
        bridge.IndexJson = refreshedIndex.Json;
        toolServer.Enqueue("missing-tool-override", ToolBehavior.Echo());
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, staleIndex, bridge.BridgeUri);
        ResolutionTestSupport.WriteToolUrls(
            sandbox,
            new Dictionary<string, string>
            {
                ["cli-cloud-bridge"] = bridge.BridgeUri.ToString(),
                ["to-lowercase"] = toolServer.ToolUri("missing-tool-override").ToString(),
            });
        ResolutionTestSupport.SeedProject(sandbox);

        var result = await cli.Run(
            ["to-lowercase", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("CLOUD-BRIDGE CALL TRIGGERED", result.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("Refreshing CLI tool URL index", result.Stdout, StringComparison.Ordinal);
        Assert.Contains(
            "cli:> to-lowercase [user-set]",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Empty(bridge.Requests);
        Assert.Equal("missing-tool-override", Assert.Single(toolServer.Requests).ToolName);
    }

    [Fact(DisplayName = "res-refresh-empty-index: empty index triggers a refresh")]
    public async Task EmptyIndexTriggersRefreshAndThenRunsTool()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        var index = IndexFixture.Load(toolServer);
        bridge.IndexJson = index.Json;
        toolServer.Enqueue("to-uppercase", ToolBehavior.Echo());
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index, bridge.BridgeUri);
        sandbox.WriteHomeFile(
            ".ssotme/remote_tools/ssotme-tools.json",
            """{"transpilerVersions":{}}""");
        ResolutionTestSupport.SeedProject(sandbox);

        var result = await cli.Run(
            ["to-uppercase", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "CLOUD-BRIDGE CALL TRIGGERED: Empty local transpiler URL cache",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Contains(
            "[cli] Refreshing CLI tool URL index...",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Single(bridge.Requests);
        Assert.Single(toolServer.Requests);
    }

    [Fact(DisplayName = "res-bootstrap-first-run: first run bootstraps the sandbox home")]
    public async Task FirstRunBootstrapsHomeThroughConfiguredLocalBridge()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        var index = IndexFixture.Load(toolServer);
        bridge.IndexJson = index.Json;
        toolServer.Enqueue("to-uppercase", ToolBehavior.Echo());
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedProject(sandbox);

        var configure = await cli.Run(
            ["-setUrl", $"cli-cloud-bridge={bridge.BridgeUri}"],
            sandbox.ProjectPath,
            sandbox);
        var result = await cli.Run(
            ["to-uppercase", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, configure.ExitCode);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "CLOUD-BRIDGE CALL TRIGGERED: Empty local transpiler URL cache",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Single(bridge.Requests);
        Assert.Single(toolServer.Requests);

        var configRoot = Path.Combine(sandbox.HomePath, ".ssotme");
        Assert.True(File.Exists(Path.Combine(configRoot, "tool_urls.json")));
        Assert.True(File.Exists(Path.Combine(configRoot, "remote_tools", "ssotme-tools.json")));
        Assert.True(File.Exists(Path.Combine(configRoot, "remote_tools", "cli_version")));
        Assert.True(File.Exists(Path.Combine(configRoot, "remote_tools", "effortless.json")));
    }

    [Fact(
        DisplayName = "res-dead-bridge-recovery: dead bridge resets to bootstrap URL",
        Skip = "The legacy DLL retries a hardcoded external HTTPS bootstrap URL; deterministic CI requires a product seam that Step 1 forbids.")]
    [Trait("Slow", "true")]
    public void DeadBridgeRecoveryRequiresTheHardcodedExternalService()
    {
    }

    [Fact(DisplayName = "res-no-refresh-on-version-change: CLI version change alone does not refresh")]
    public async Task VersionMarkerMismatchAloneDoesNotRefresh()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        var index = IndexFixture.Load(toolServer);
        bridge.IndexJson = index.Json;
        toolServer.Enqueue("to-uppercase", ToolBehavior.Echo());
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index, bridge.BridgeUri);
        sandbox.WriteHomeFile(".ssotme/remote_tools/cli_version", "0000");
        ResolutionTestSupport.SeedProject(sandbox);

        var result = await cli.Run(
            ["to-uppercase", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("CLOUD-BRIDGE CALL TRIGGERED", result.Stdout, StringComparison.Ordinal);
        Assert.Empty(bridge.Requests);
        Assert.Single(toolServer.Requests);
        Assert.Equal(
            "0000",
            File.ReadAllText(
                Path.Combine(
                    sandbox.HomePath,
                    ".ssotme",
                    "remote_tools",
                    "cli_version")));
    }

    [Fact(DisplayName = "res-refresh-tools: refreshTools re-fetches")]
    public async Task RefreshToolsAliasesPurgeAndRefetchIndex()
    {
        foreach (var alias in new[] { "-refreshTools", "refreshtools", "-rt" })
        {
            var cli = new CliUnderTest();
            await using var toolServer = new MockToolServer();
            await using var bridge = new ResolutionBridgeServer();
            var index = IndexFixture.Load(toolServer);
            bridge.IndexJson = index.Json;
            using var sandbox = Sandbox.Create(cli);
            ResolutionTestSupport.SeedHome(sandbox, index, bridge.BridgeUri);
            sandbox.WriteHomeFile(".ssotme/bridge_version_index", "9");

            var result = await cli.Run([alias], sandbox.ProjectPath, sandbox);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                "CLOUD-BRIDGE CALL TRIGGERED: RefreshRemoteTools: explicit -refreshTools invocation",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.Contains(
                "[cli] Remote tools index refreshed.",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.Single(bridge.Requests);
            Assert.False(
                File.Exists(
                    Path.Combine(sandbox.HomePath, ".ssotme", "bridge_version_index")));
            var persisted = File.ReadAllText(
                Path.Combine(
                    sandbox.HomePath,
                    ".ssotme",
                    "remote_tools",
                    "ssotme-tools.json"));
            Assert.Contains(
                "effortless/common/to-uppercase",
                persisted,
                StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "res-bridge-self-update: latestBridgeVersion updates tool_urls")]
    public async Task BridgeSelfUpdateAdvancesButDoesNotDowngradeItsIndex()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        var index = IndexFixture.Load(toolServer);
        bridge.IndexJson = WithBridgeVersion(index.Json, bridge.BridgeUri, 11);
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index, bridge.BridgeUri);

        var upgraded = await cli.Run(["-refreshTools"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, upgraded.ExitCode);
        Assert.Equal(
            bridge.BridgeUri.ToString(),
            ResolutionTestSupport.ReadHomeObject(
                sandbox,
                ".ssotme/tool_urls.json")["cli-cloud-bridge"]?.GetValue<string>());
        Assert.Equal(
            "11",
            File.ReadAllText(
                Path.Combine(sandbox.HomePath, ".ssotme", "bridge_version_index")));

        bridge.IndexJson = WithBridgeVersion(index.Json, bridge.BridgeUri, 10);
        sandbox.WriteHomeFile(
            ".ssotme/remote_tools/ssotme-tools.json",
            """{"transpilerVersions":{}}""");
        ResolutionTestSupport.SeedProject(sandbox);
        toolServer.Enqueue("to-uppercase", ToolBehavior.Echo());

        var lower = await cli.Run(
            ["to-uppercase", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, lower.ExitCode);
        Assert.Equal(2, bridge.Requests.Count);
        Assert.Equal(
            "11",
            File.ReadAllText(
                Path.Combine(sandbox.HomePath, ".ssotme", "bridge_version_index")));
    }

    [Fact(DisplayName = "res-bridge-payload: bridge is called with cli_version")]
    public async Task BridgeRefreshPayloadIncludesCliVersionAndRemoteToolsProjectName()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        var index = IndexFixture.Load(toolServer);
        bridge.IndexJson = index.Json;
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index, bridge.BridgeUri);

        var result = await cli.Run(["-refreshTools"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        var request = Assert.Single(bridge.Requests);
        Assert.Contains(
            $"cli_version={CliUnderTest.PackageVersion}",
            request.CliParams);
        Assert.Contains("project-name=remote_tools", request.CliParams);
        Assert.NotNull(request.CliTranspiler);
        Assert.NotNull(request.TranspilerName);
    }

    [Fact(DisplayName = "res-list-versions: listVersions output")]
    public async Task ListVersionsAliasesShowSortedVersionsAndOverride()
    {
        foreach (var alias in new[] { "-listVersions", "-lv", "-list", "-l" })
        {
            var cli = new CliUnderTest();
            await using var server = new MockToolServer();
            var index = IndexFixture.Load(server);
            using var sandbox = Sandbox.Create(cli);
            ResolutionTestSupport.SeedHome(sandbox, index);
            ResolutionTestSupport.WriteToolUrls(
                sandbox,
                new Dictionary<string, string>
                {
                    ["cli-cloud-bridge"] = index.BridgeUri.ToString(),
                    ["to-uppercase"] = "http://localhost:43210/local/",
                });

            var result = await cli.Run(
                ["to-uppercase", alias],
                sandbox.ProjectPath,
                sandbox);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                "Available versions for effortless/common/to-uppercase:",
                result.Stdout,
                StringComparison.Ordinal);
            var headIndex = result.Stdout.IndexOf(
                $"  {ResolutionTestSupport.HeadVersion} (latest)",
                StringComparison.Ordinal);
            var oldIndex = result.Stdout.IndexOf(
                $"  {ResolutionTestSupport.OldVersion}",
                StringComparison.Ordinal);
            Assert.True(headIndex >= 0 && oldIndex > headIndex, result.Stdout);
            Assert.Contains(
                $"url: {server.ToolUri("to-uppercase", ResolutionTestSupport.HeadVersion)}",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.Contains(
                "* globally overridden via effortless -setUrl to-uppercase=http://localhost:43210/local/",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.Contains(
                "run 'effortless -removeUrl to-uppercase' to reset",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.Contains(
                "Run with: effortless to-uppercase/<versionKey>",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.Contains(
                "Run latest: effortless to-uppercase",
                result.Stdout,
                StringComparison.Ordinal);
            Golden.AssertMatches(
                "res-list-versions",
                Golden.Normalize(result.Stdout, sandbox, server.BaseUri.ToString()));
        }
    }

    [Fact(DisplayName = "res-list-versions-missing: listVersions miss")]
    public async Task ListVersionsMissingPrintsLegacySystemHint()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server);
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index);

        var result = await cli.Run(
            ["nope", "-listVersions"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "No versions for 'nope' were found in the remote tools index. It may still exist in our legacy system.",
            result.Stdout,
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "res-upgrade-tool: upgrade clears the pin")]
    public async Task UpgradeToolAliasesClearPinWithoutRunningTool()
    {
        var forms = new[]
        {
            new[] { "upgrade", "to-uppercase" },
            new[] { "to-uppercase", "-upgrade" },
            new[] { "unpin", "to-uppercase" },
        };

        foreach (var form in forms)
        {
            var cli = new CliUnderTest();
            await using var toolServer = new MockToolServer();
            await using var bridge = new ResolutionBridgeServer();
            var index = IndexFixture.Load(toolServer);
            bridge.IndexJson = index.Json;
            using var sandbox = Sandbox.Create(cli);
            ResolutionTestSupport.SeedHome(sandbox, index, bridge.BridgeUri);
            ResolutionTestSupport.SeedProject(
                sandbox,
                new ResolutionProjectStep(
                    "To Uppercase",
                    "",
                    "to-uppercase -i in.txt",
                    ResolutionTestSupport.OldVersion));

            var result = await cli.Run(form, sandbox.ProjectPath, sandbox);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                "CLOUD-BRIDGE CALL TRIGGERED: RefreshRemoteTools: explicit -refreshTools invocation",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.Contains(
                $"Upgraded to-uppercase: {ResolutionTestSupport.OldVersion} → HEAD ({ResolutionTestSupport.HeadVersion}, unpinned — will track latest)",
                result.Stdout,
                StringComparison.Ordinal);
            var step = ResolutionTestSupport.FindStep(
                ResolutionTestSupport.ReadProject(sandbox),
                "to-uppercase");
            Assert.Null(step["PinnedVersion"]);
            Assert.Equal(
                ResolutionTestSupport.HeadVersion,
                step["LastVersionUsed"]?.GetValue<string>());
            Assert.Empty(toolServer.Requests);
            Assert.Single(bridge.Requests);
        }
    }

    [Fact(DisplayName = "res-upgrade-not-installed: upgrade of an uninstalled tool")]
    public async Task UpgradeUninstalledToolRefreshesAndExitsSuccessfully()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        var index = IndexFixture.Load(toolServer);
        bridge.IndexJson = index.Json;
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index, bridge.BridgeUri);
        ResolutionTestSupport.SeedProject(sandbox);

        var result = await cli.Run(
            ["upgrade", "to-uppercase"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "to-uppercase is not used in this project — nothing to unpin here.",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Contains(
            $"Refreshed the core tools index; 'to-uppercase' will track latest (HEAD {ResolutionTestSupport.HeadVersion}) wherever it is used unpinned.",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Single(bridge.Requests);
        Assert.Empty(toolServer.Requests);
    }

    [Fact(DisplayName = "res-upgrade-no-project: upgrade outside a project")]
    public async Task UpgradeOutsideProjectReportsNoProjectAfterRefresh()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        var index = IndexFixture.Load(toolServer);
        bridge.IndexJson = index.Json;
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index, bridge.BridgeUri);

        var result = await cli.Run(
            ["upgrade", "to-uppercase"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "No effortless.json project found in this directory.",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Single(bridge.Requests);
        Assert.Empty(toolServer.Requests);
    }

    [Fact(DisplayName = "res-upgrade-all: upgradeAll")]
    public async Task UpgradeAllAliasesReportUpOkAndSkipCounts()
    {
        var forms = new[]
        {
            new[] { "-upgradeAll" },
            new[] { "upgradeall" },
            new[] { "upgrade" },
        };

        foreach (var form in forms)
        {
            var cli = new CliUnderTest();
            await using var toolServer = new MockToolServer();
            await using var bridge = new ResolutionBridgeServer();
            var index = IndexFixture.Load(toolServer);
            bridge.IndexJson = index.Json;
            using var sandbox = Sandbox.Create(cli);
            ResolutionTestSupport.SeedHome(sandbox, index, bridge.BridgeUri);
            ResolutionTestSupport.SeedProject(
                sandbox,
                new ResolutionProjectStep(
                    "To Uppercase",
                    "",
                    "to-uppercase -i in.txt",
                    ResolutionTestSupport.OldVersion),
                new ResolutionProjectStep(
                    "Echo",
                    "",
                    "echo -i in.txt",
                    LastVersionUsed: ResolutionTestSupport.HeadVersion),
                new ResolutionProjectStep(
                    "Execute",
                    "",
                    "-execute echo local"),
                new ResolutionProjectStep(
                    "Unknown",
                    "",
                    "unknown-tool -i in.txt",
                    "v1"));

            var result = await cli.Run(form, sandbox.ProjectPath, sandbox);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                $"  UP   to-uppercase: {ResolutionTestSupport.OldVersion} → HEAD ({ResolutionTestSupport.HeadVersion}, unpinned)",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.Contains(
                $"  OK   echo — already unpinned at HEAD ({ResolutionTestSupport.HeadVersion})",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.DoesNotContain("-execute", result.Stdout, StringComparison.Ordinal);
            Assert.Contains(
                "  SKIP unknown-tool — not found in remote tools index",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.Contains("Upgraded 1 tool(s), skipped 1.", result.Stdout, StringComparison.Ordinal);
            Assert.Single(bridge.Requests);
            Assert.Empty(toolServer.Requests);
        }
    }

    [Fact(DisplayName = "res-user-set-override: tool_urls override beats the index")]
    public async Task UserOverrideBeatsIndexAndPreservesResolvedVersionMetadata()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server);
        var overrideUri = server.ToolUri("override-uppercase");
        server.Enqueue("override-uppercase", ToolBehavior.Echo());
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index);
        ResolutionTestSupport.WriteToolUrls(
            sandbox,
            new Dictionary<string, string>
            {
                ["cli-cloud-bridge"] = index.BridgeUri.ToString(),
                ["to-uppercase"] = overrideUri.ToString(),
            });
        ResolutionTestSupport.SeedProject(
            sandbox,
            new ResolutionProjectStep(
                "To Uppercase",
                "",
                "to-uppercase -i in.txt",
                LastVersionUsed: ResolutionTestSupport.HeadVersion));

        var result = await cli.Run(
            ["to-uppercase", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        var request = Assert.Single(server.Requests);
        Assert.Equal("override-uppercase", request.ToolName);
        Assert.Contains("cli:> to-uppercase [user-set]", result.Stdout, StringComparison.Ordinal);
        var step = ResolutionTestSupport.FindStep(
            ResolutionTestSupport.ReadProject(sandbox),
            "to-uppercase");
        Assert.Equal(
            ResolutionTestSupport.HeadVersion,
            step["LastVersionUsed"]?.GetValue<string>());
    }

    [Fact(DisplayName = "res-user-set-unknown-tool: tool_urls-only tool")]
    public async Task UserSetUnknownToolResolvesWithoutLegacyRefresh()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        var index = IndexFixture.Load(toolServer);
        bridge.IndexJson = index.Json;
        var userUri = toolServer.ToolUri("mytool");
        toolServer.Enqueue("mytool", ToolBehavior.Echo());
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index, bridge.BridgeUri);
        ResolutionTestSupport.WriteToolUrls(
            sandbox,
            new Dictionary<string, string>
            {
                ["cli-cloud-bridge"] = bridge.BridgeUri.ToString(),
                ["mytool"] = userUri.ToString(),
            });
        ResolutionTestSupport.SeedProject(sandbox);

        var result = await cli.Run(
            ["mytool", "-i", "in.txt"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("cli:> mytool [user-set]", result.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("CLOUD-BRIDGE CALL TRIGGERED", result.Stdout, StringComparison.Ordinal);
        Assert.Empty(bridge.Requests);
        Assert.Equal("mytool", Assert.Single(toolServer.Requests).ToolName);
    }

    [Fact(DisplayName = "res-freshness-offline-meta: offline metadata commands do not refresh")]
    public async Task OfflineMetadataCommandsDoNotRefreshMissingCatalog()
    {
        var cli = new CliUnderTest();
        await using var bridge = new ResolutionBridgeServer();
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.WriteToolUrls(
            sandbox,
            new Dictionary<string, string>
            {
                ["cli-cloud-bridge"] = bridge.BridgeUri.ToString(),
            });

        var help = await cli.Run(["-help"], sandbox.ProjectPath, sandbox);
        var version = await cli.Run(["-version"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, help.ExitCode);
        Assert.Contains("SSoTme CLI", help.Stdout, StringComparison.Ordinal);
        Assert.Equal(0, version.ExitCode);
        Assert.Equal(CliUnderTest.PackageVersion + Environment.NewLine, version.Stdout);
        Assert.Empty(bridge.Requests);
        Assert.False(
            File.Exists(
                Path.Combine(
                    sandbox.HomePath,
                    ".ssotme",
                    "remote_tools",
                    "ssotme-tools.json")));
    }

    private static string WithBridgeVersion(string indexJson, Uri bridgeUri, long versionIndex)
    {
        var root = JsonNode.Parse(indexJson)?.AsObject()
            ?? throw new InvalidDataException("Index fixture is not a JSON object.");
        root["cliUpdateAvailable"] = null;
        root["latestBridgeVersion"] = new JsonObject
        {
            ["name"] = "cli-cloud-bridge",
            ["version"] = $"v{versionIndex}",
            ["url"] = bridgeUri.ToString().TrimEnd('/'),
            ["versionIndex"] = versionIndex,
        };
        return root.ToJsonString();
    }
}
