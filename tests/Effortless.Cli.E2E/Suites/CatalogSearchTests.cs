using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

/// <summary>
/// Step 11: searchable catalog (listTools/searchTools modifiers, -json, info
/// catalog age) and R11 refresh-on-complete-timeout.
/// </summary>
public sealed class CatalogSearchTests
{
    [Fact(DisplayName = "catalog-search-text: searchTools matches descriptions and tags, not just names")]
    public async Task SearchMatchesDescriptionAndTags()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        using var sandbox = SeedSearchCatalog(cli, toolServer, bridge);

        var byDescription = await cli.Run(["searchTools", "verbatim"], sandbox.ProjectPath, sandbox);
        var byTag = await cli.Run(["-searchTools", "sample"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, byDescription.ExitCode);
        Assert.Equal(0, byTag.ExitCode);
        Assert.Contains("Tools matching 'verbatim' (1):", byDescription.Stdout, StringComparison.Ordinal);
        Assert.Contains("effortless/common/echo", byDescription.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("pg-tool", byDescription.Stdout, StringComparison.Ordinal);
        Assert.Contains("Tools matching 'sample' (1):", byTag.Stdout, StringComparison.Ordinal);
        Assert.Contains("effortless/common/echo", byTag.Stdout, StringComparison.Ordinal);
        Assert.Empty(bridge.Requests);
    }

    [Fact(DisplayName = "catalog-search-multi-term: every search term must match")]
    public async Task SearchTermsAreAnded()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        using var sandbox = SeedSearchCatalog(cli, toolServer, bridge);

        var match = await cli.Run(["searchTools", "echo verbatim"], sandbox.ProjectPath, sandbox);
        var miss = await cli.Run(["searchTools", "echo nomatch"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, match.ExitCode);
        Assert.Contains("Tools matching 'echo verbatim' (1):", match.Stdout, StringComparison.Ordinal);
        Assert.Equal(0, miss.ExitCode);
        Assert.Equal($"No tools matched 'echo nomatch'.{Environment.NewLine}", miss.Stdout);
        Assert.Empty(bridge.Requests);
    }

    [Fact(DisplayName = "catalog-filter-category: -category keeps one category segment")]
    public async Task CategoryFilterUsesTheMiddleSegment()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        using var sandbox = SeedSearchCatalog(cli, toolServer, bridge);

        var sql = await cli.Run(["listTools", "-category", "sql"], sandbox.ProjectPath, sandbox);
        var common = await cli.Run(["-listTools", "-category", "COMMON"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, sql.ExitCode);
        Assert.Contains("Available tools (1):", sql.Stdout, StringComparison.Ordinal);
        Assert.Contains("acme/sql/pg-tool", sql.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("effortless/common", sql.Stdout, StringComparison.Ordinal);
        Assert.Equal(0, common.ExitCode);
        Assert.Contains("Available tools (2):", common.Stdout, StringComparison.Ordinal);
        Assert.Contains("effortless/common/echo", common.Stdout, StringComparison.Ordinal);
        Assert.Contains("effortless/common/to-uppercase", common.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("pg-tool", common.Stdout, StringComparison.Ordinal);
        Assert.Empty(bridge.Requests);
    }

    [Fact(DisplayName = "catalog-filter-updated-since: -updatedSince filters on head createdAt")]
    public async Task UpdatedSinceFiltersOnHeadCreatedAt()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        using var sandbox = SeedSearchCatalog(cli, toolServer, bridge);

        var recent = await cli.Run(["listTools", "-updatedSince", "2026-06-01"], sandbox.ProjectPath, sandbox);
        var invalid = await cli.Run(["listTools", "-updatedSince", "not-a-date"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, recent.ExitCode);
        Assert.Contains("Available tools (1):", recent.Stdout, StringComparison.Ordinal);
        Assert.Contains("acme/sql/pg-tool", recent.Stdout, StringComparison.Ordinal);
        Assert.Contains("2026-08-15", recent.Stdout, StringComparison.Ordinal);
        Assert.True(invalid.Failed);
        Assert.Contains("Invalid -updatedSince value 'not-a-date'", invalid.Combined, StringComparison.Ordinal);
        Assert.Empty(bridge.Requests);
    }

    [Fact(DisplayName = "catalog-sort-popular: -sort popular orders by monthlyRequestCount")]
    public async Task SortPopularOrdersByMonthlyRequests()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        using var sandbox = SeedSearchCatalog(cli, toolServer, bridge);

        var popular = await cli.Run(["listTools", "-sort", "popular"], sandbox.ProjectPath, sandbox);
        var updated = await cli.Run(["listTools", "-sort", "updated", "-headOnly", "-requiresKey", "false"], sandbox.ProjectPath, sandbox);
        var bogus = await cli.Run(["listTools", "-sort", "bogus"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, popular.ExitCode);
        var pg = popular.Stdout.IndexOf("acme/sql/pg-tool", StringComparison.Ordinal);
        var echo = popular.Stdout.IndexOf("effortless/common/echo", StringComparison.Ordinal);
        var upper = popular.Stdout.IndexOf("effortless/common/to-uppercase", StringComparison.Ordinal);
        Assert.True(pg >= 0 && pg < echo && echo < upper, popular.Stdout);
        Assert.Equal(0, updated.ExitCode);
        Assert.Contains("Available tools (2):", updated.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("effortless/common/echo", updated.Stdout, StringComparison.Ordinal);
        Assert.True(
            updated.Stdout.IndexOf("acme/sql/pg-tool", StringComparison.Ordinal)
            < updated.Stdout.IndexOf("effortless/common/to-uppercase", StringComparison.Ordinal));
        Assert.True(bogus.Failed);
        Assert.Contains("Invalid -sort value 'bogus'", bogus.Combined, StringComparison.Ordinal);
        Assert.Empty(bridge.Requests);
    }

    [Fact(DisplayName = "catalog-filter-head-only: -headOnly hides tools without a head version")]
    public async Task HeadOnlyHidesNoHeadTools()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        using var sandbox = SeedSearchCatalog(cli, toolServer, bridge);
        var root = ResolutionTestSupport.ReadHomeObject(sandbox, ".effortless/remote_tools/effortless-tools.json");
        root["transpilerVersions"]!["aaa/example/no-head"] = new JsonObject
        {
            ["v2020.01.01.0000"] = new JsonObject
            {
                ["metaData"] = new JsonObject { ["isHeadVersion"] = false },
                ["urls"] = new JsonObject { ["post"] = "http://127.0.0.1:1/" },
            },
        };
        sandbox.WriteHomeFile(".effortless/remote_tools/effortless-tools.json", root.ToJsonString());

        var all = await cli.Run(["listTools"], sandbox.ProjectPath, sandbox);
        var headed = await cli.Run(["listTools", "-headOnly"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, all.ExitCode);
        Assert.Contains("Available tools (4):", all.Stdout, StringComparison.Ordinal);
        Assert.Contains("NO HEAD", all.Stdout, StringComparison.Ordinal);
        Assert.Equal(0, headed.ExitCode);
        Assert.Contains("Available tools (3):", headed.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("no-head", headed.Stdout, StringComparison.Ordinal);
        Assert.Empty(bridge.Requests);
    }

    [Fact(DisplayName = "catalog-filter-requires-key: -requiresKey true|false filters on the head requiresAPIKey flag")]
    public async Task RequiresKeyFiltersOnHeadFlag()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        using var sandbox = SeedSearchCatalog(cli, toolServer, bridge);

        var keyed = await cli.Run(["listTools", "-requiresKey", "true"], sandbox.ProjectPath, sandbox);
        var open = await cli.Run(["listTools", "-requiresKey", "false"], sandbox.ProjectPath, sandbox);
        var invalid = await cli.Run(["listTools", "-requiresKey", "maybe"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, keyed.ExitCode);
        Assert.Contains("Available tools (1):", keyed.Stdout, StringComparison.Ordinal);
        Assert.Contains("effortless/common/echo", keyed.Stdout, StringComparison.Ordinal);
        Assert.Equal(0, open.ExitCode);
        Assert.Contains("Available tools (2):", open.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("effortless/common/echo", open.Stdout, StringComparison.Ordinal);
        Assert.True(invalid.Failed);
        Assert.Contains("Invalid -requiresKey value 'maybe'", invalid.Combined, StringComparison.Ordinal);
        Assert.Empty(bridge.Requests);
    }

    [Fact(DisplayName = "catalog-json: -json emits the filtered entries as a JSON array")]
    public async Task JsonEmitsFilteredEntries()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        using var sandbox = SeedSearchCatalog(cli, toolServer, bridge);

        var result = await cli.Run(["searchTools", "echo", "-json"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("NAME", result.Stdout, StringComparison.Ordinal);
        var array = JsonNode.Parse(result.Stdout)!.AsArray();
        var entry = Assert.Single(array)!.AsObject();
        Assert.Equal("effortless/common/echo", entry["canonicalName"]?.GetValue<string>());
        Assert.Equal("echo", entry["shortName"]?.GetValue<string>());
        Assert.Equal("effortless", entry["account"]?.GetValue<string>());
        Assert.Equal("common", entry["category"]?.GetValue<string>());
        Assert.Equal("v2026.01.01.0001", entry["headVersion"]?.GetValue<string>());
        Assert.Equal("2026-01-01T00:00:00Z", entry["headCreatedAt"]?.GetValue<string>());
        Assert.Equal(2, entry["versionCount"]?.GetValue<int>());
        Assert.True(entry["requiresApiKey"]?.GetValue<bool>());
        Assert.Equal(40, entry["monthlyRequestCount"]?.GetValue<long>());
        Assert.Equal(
            "Repeats its input verbatim so the wire contract can be inspected.",
            entry["description"]?.GetValue<string>());
        Assert.Equal(
            new[] { "debug", "sample" },
            entry["tags"]!.AsArray().Select(tag => tag!.GetValue<string>()).ToArray());
        Assert.Empty(bridge.Requests);
    }

    [Fact(DisplayName = "catalog-info-shows-age: info prints the catalog age and next refresh")]
    public async Task InfoShowsCatalogAge()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        using var sandbox = SeedSearchCatalog(cli, toolServer, bridge);
        var fetchedAt = CliUnderTest.TestUtcNow - new TimeSpan(3, 12, 0);
        SetFetchedAt(sandbox, fetchedAt);

        var result = await cli.Run(["-info"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            $"Catalog: fetched {fetchedAt.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}, age 3h 12m, next automatic refresh {(fetchedAt + TimeSpan.FromHours(24)).UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Empty(bridge.Requests);
    }

    [Fact(DisplayName = "timeout-forces-one-refresh: a catalog-resolved tool that is completely unreachable forces exactly one catalog refresh")]
    public async Task CompleteTimeoutForcesExactlyOneRefresh()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        var closedPort = ReserveClosedPort();
        var index = IndexFixture.Load(toolServer);
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index, bridge.BridgeUri);
        var deadCatalog = PointHeadAt(index.Json, "effortless/common/to-uppercase", $"http://127.0.0.1:{closedPort}/tools/to-uppercase/v2026.01.01.0001/");
        WriteCatalog(sandbox, deadCatalog);
        bridge.IndexJson = deadCatalog;
        ResolutionTestSupport.SeedProject(
            sandbox,
            new ResolutionProjectStep("To Uppercase", "", "to-uppercase -i in.txt -waitTimeout 15000"));

        var result = await cli.Run(["build"], sandbox.ProjectPath, sandbox, timeoutMs: 120_000);

        Assert.True(result.Failed);
        Assert.Contains("did not respond (connection refused); refreshing the catalog", result.Stdout, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(result.Stdout, "CLOUD-BRIDGE CALL TRIGGERED"));
        Assert.Contains("to-uppercase is offline at http://127.0.0.1:", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("catalog age", result.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("moved; retrying", result.Stdout, StringComparison.Ordinal);
        Assert.Single(bridge.Requests);
        Assert.Empty(toolServer.Requests);
    }

    [Fact(DisplayName = "timeout-retries-once-on-moved-url: when the refreshed catalog moves the head URL the step is retried once and succeeds")]
    public async Task MovedHeadUrlIsRetriedOnce()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        var closedPort = ReserveClosedPort();
        var index = IndexFixture.Load(toolServer);
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index, bridge.BridgeUri);
        WriteCatalog(sandbox, PointHeadAt(index.Json, "effortless/common/to-uppercase", $"http://127.0.0.1:{closedPort}/tools/to-uppercase/v2026.01.01.0001/"));
        bridge.IndexJson = index.Json;
        toolServer.Enqueue("to-uppercase", ToolBehavior.Echo());
        ResolutionTestSupport.SeedProject(
            sandbox,
            new ResolutionProjectStep("To Uppercase", "", "to-uppercase -i in.txt -waitTimeout 15000"));

        var result = await cli.Run(["build"], sandbox.ProjectPath, sandbox, timeoutMs: 120_000);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            $"Tool to-uppercase moved; retrying against {toolServer.ToolUri("to-uppercase")}",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(result.Stdout, "CLOUD-BRIDGE CALL TRIGGERED"));
        Assert.Single(bridge.Requests);
        Assert.Equal("to-uppercase", Assert.Single(toolServer.Requests).ToolName);
        var step = ResolutionTestSupport.FindStep(ResolutionTestSupport.ReadProject(sandbox), "to-uppercase");
        Assert.StartsWith(toolServer.BaseUri.ToString(), step["LastUrl"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "timeout-does-not-refresh-for-target-url: a -targetUrl or tool_urls override that times out never refreshes the catalog")]
    public async Task OverridesNeverForceARefresh()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        var closedPort = ReserveClosedPort();
        var closedUrl = $"http://127.0.0.1:{closedPort}/tools/to-uppercase/v2026.01.01.0001/";
        var index = IndexFixture.Load(toolServer);
        bridge.IndexJson = index.Json;
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index, bridge.BridgeUri);
        ResolutionTestSupport.SeedProject(sandbox);

        var targetUrl = await cli.Run(
            ["to-uppercase", "-targetUrl", closedUrl, "-i", "in.txt", "-waitTimeout", "15000"],
            sandbox.ProjectPath,
            sandbox,
            timeoutMs: 120_000);

        ResolutionTestSupport.WriteToolUrls(
            sandbox,
            new Dictionary<string, string>
            {
                ["cli-cloud-bridge"] = bridge.BridgeUri.ToString(),
                ["to-uppercase"] = closedUrl,
            });
        var toolUrls = await cli.Run(
            ["to-uppercase", "-i", "in.txt", "-waitTimeout", "15000"],
            sandbox.ProjectPath,
            sandbox,
            timeoutMs: 120_000);

        Assert.True(targetUrl.Failed);
        Assert.True(toolUrls.Failed);
        Assert.DoesNotContain("CLOUD-BRIDGE CALL TRIGGERED", targetUrl.Stdout + toolUrls.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("refreshing the catalog", targetUrl.Stdout + toolUrls.Stdout, StringComparison.Ordinal);
        Assert.Contains("Connection refused", targetUrl.Combined, StringComparison.Ordinal);
        Assert.Contains("Connection refused", toolUrls.Combined, StringComparison.Ordinal);
        Assert.Empty(bridge.Requests);
        Assert.Empty(toolServer.Requests);
    }

    [Fact(DisplayName = "timeout-second-step-does-not-refresh-again: a second unreachable step in the same build does not force a second refresh")]
    public async Task SecondUnreachableStepDoesNotRefreshAgain()
    {
        var cli = new CliUnderTest();
        await using var toolServer = new MockToolServer();
        await using var bridge = new ResolutionBridgeServer();
        var closedPort = ReserveClosedPort();
        var index = IndexFixture.Load(toolServer);
        using var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index, bridge.BridgeUri);
        var deadCatalog = PointHeadAt(
            PointHeadAt(index.Json, "effortless/common/to-uppercase", $"http://127.0.0.1:{closedPort}/tools/to-uppercase/v2026.01.01.0001/"),
            "effortless/common/echo",
            $"http://127.0.0.1:{closedPort}/tools/echo/v2026.01.01.0001/");
        WriteCatalog(sandbox, deadCatalog);
        bridge.IndexJson = deadCatalog;
        ResolutionTestSupport.SeedProject(
            sandbox,
            new ResolutionProjectStep("To Uppercase", "", "to-uppercase -i in.txt -waitTimeout 15000"),
            new ResolutionProjectStep("Echo", "", "echo -i in.txt -waitTimeout 15000"));

        var result = await cli.Run(
            ["build", "-continueOnError"],
            sandbox.ProjectPath,
            sandbox,
            timeoutMs: 180_000);

        // continue-on-error builds exit 0 (build-coe-continues); the failures
        // are in errors.json and the summary block.
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("-continueOnError is set", result.Stdout, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(result.Stdout, "CLOUD-BRIDGE CALL TRIGGERED"));
        Assert.Equal(1, CountOccurrences(result.Stdout, "refreshing the catalog"));
        Assert.Contains("to-uppercase is offline at", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("echo is offline at", result.Stdout, StringComparison.Ordinal);
        Assert.Single(bridge.Requests);
        Assert.Empty(toolServer.Requests);
    }

    private static Sandbox SeedSearchCatalog(
        CliUnderTest cli,
        MockToolServer toolServer,
        ResolutionBridgeServer bridge)
    {
        var index = IndexFixture.Load(toolServer, "catalog-search");
        bridge.IndexJson = index.Json;
        var sandbox = Sandbox.Create(cli);
        ResolutionTestSupport.SeedHome(sandbox, index, bridge.BridgeUri);
        return sandbox;
    }

    private static void SetFetchedAt(Sandbox sandbox, DateTimeOffset value)
    {
        var root = ResolutionTestSupport.ReadHomeObject(
            sandbox,
            ".effortless/remote_tools/effortless-tools.json");
        root["fetchedAt"] = value.ToString("O");
        sandbox.WriteHomeFile(".effortless/remote_tools/effortless-tools.json", root.ToJsonString());
    }

    private static void WriteCatalog(Sandbox sandbox, string catalogJson)
    {
        var root = JsonNode.Parse(catalogJson)!.AsObject();
        root["fetchedAt"] = CliUnderTest.TestUtcNow.ToString("O");
        sandbox.WriteHomeFile(
            ".effortless/remote_tools/effortless-tools.json",
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string PointHeadAt(string catalogJson, string tool, string url)
    {
        var root = JsonNode.Parse(catalogJson)!.AsObject();
        var versions = root["transpilerVersions"]![tool]!.AsObject();
        foreach (var version in versions)
        {
            if (version.Value?["metaData"]?["isHeadVersion"]?.GetValue<bool>() == true)
            {
                version.Value!["urls"]!["post"] = url;
            }
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static int ReserveClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        for (var index = text.IndexOf(needle, StringComparison.Ordinal);
             index >= 0;
             index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
