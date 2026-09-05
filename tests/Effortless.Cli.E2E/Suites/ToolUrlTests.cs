using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

public sealed class ToolUrlTests
{
    [Fact(DisplayName = "urls-set: setUrl writes tool_urls.json")]
    public async Task SetUrlWritesUserConfiguration()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);

        var first = await cli.Run(["-setUrl", "a=http://x"], sandbox.ProjectPath, sandbox);
        var second = await cli.Run(["seturl", "b=y.com"], sandbox.ProjectPath, sandbox);
        var third = await cli.Run(["-su", "c=https://z"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.Equal(0, third.ExitCode);
        Assert.Contains("Tool 'a' URL set to: http://x", first.Stdout, StringComparison.Ordinal);
        Assert.Contains("Tool 'b' URL set to: https://y.com", second.Stdout, StringComparison.Ordinal);
        Assert.Contains("Tool 'c' URL set to: https://z", third.Stdout, StringComparison.Ordinal);

        var urls = ToolUrlTestSupport.ReadToolUrls(sandbox);
        Assert.Equal("http://x", urls["a"]?.GetValue<string>());
        Assert.Equal("https://y.com", urls["b"]?.GetValue<string>());
        Assert.Equal("https://z", urls["c"]?.GetValue<string>());
    }

    [Fact(DisplayName = "urls-set-invalid: setUrl format errors")]
    public async Task SetUrlRejectsInvalidFormats()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);

        var noEquals = await cli.Run(["-setUrl", "noequals"], sandbox.ProjectPath, sandbox);
        var tooManyEquals = await cli.Run(["-setUrl", "a=b=c"], sandbox.ProjectPath, sandbox);
        var missingName = await cli.Run(["-setUrl", "=x"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, noEquals.ExitCode);
        Assert.Equal(0, tooManyEquals.ExitCode);
        Assert.Equal(0, missingName.ExitCode);
        Assert.Contains(
            "Error: Invalid format. Usage: effortless -setUrl toolname=url",
            noEquals.Stdout,
            StringComparison.Ordinal);
        Assert.Contains(
            "Error: Invalid format. Usage: effortless -setUrl toolname=url",
            tooManyEquals.Stdout,
            StringComparison.Ordinal);
        Assert.Contains(
            "Error: Invalid format. Usage: effortless -setUrl toolname=url",
            missingName.Stdout,
            StringComparison.Ordinal);
        Assert.False(File.Exists(ToolUrlTestSupport.ToolUrlsPath(sandbox)));
    }

    [Fact(DisplayName = "urls-set-bridge-warning: overriding the bridge warns")]
    public async Task SetUrlWarnsWhenOverridingManagedBridge()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);

        var result = await cli.Run(
            ["-setUrl", "cli-cloud-bridge=http://localhost:9"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "Warning: 'cli-cloud-bridge' is an internal Effortless-managed tool. Overriding it may break remote tool resolution.",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Contains(
            "To restore default functionality, run: effortless -removeUrl cli-cloud-bridge",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Equal(
            "http://localhost:9",
            ToolUrlTestSupport.ReadToolUrls(sandbox)["cli-cloud-bridge"]?.GetValue<string>());
    }

    [Fact(DisplayName = "urls-view: viewUrl")]
    public async Task ViewUrlReportsConfiguredMissingAndMissingNameCases()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server);
        using var sandbox = Sandbox.Create(cli);
        sandbox.SeedHome(index);
        ToolUrlTestSupport.WriteMinimalProject(sandbox);
        ToolUrlTestSupport.WriteToolUrls(
            sandbox,
            new Dictionary<string, string>
            {
                ["a"] = "http://x",
                ["cli-cloud-bridge"] = index.BridgeUri.ToString(),
                ["AppStatesCsvToAppStatesXml"] =
                    server.ToolUri("missing-view-dispatch").ToString(),
            });
        server.Enqueue("missing-view-dispatch", ToolBehavior.Files());

        var flag = await cli.Run(["-viewUrl", "a"], sandbox.ProjectPath, sandbox);
        var bareword = await cli.Run(["viewurl", "a"], sandbox.ProjectPath, sandbox);
        var missingTool = await cli.Run(["-vu", "nope"], sandbox.ProjectPath, sandbox);
        var missingName = await cli.Run(["-viewUrl"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, flag.ExitCode);
        Assert.Equal(0, bareword.ExitCode);
        Assert.Contains(
            "Tool 'a' is configured with URL: http://x",
            flag.Stdout,
            StringComparison.Ordinal);
        Assert.Contains(
            "Tool 'a' is configured with URL: http://x",
            bareword.Stdout,
            StringComparison.Ordinal);
        Assert.Contains(
            "Tool 'nope' is not configured in ~/.effortless/tool_urls.json file.",
            missingTool.Stdout,
            StringComparison.Ordinal);

        // The flag form is the only safe missing-name path in legacy. The bareword
        // form falls through into the removed RabbitMQ/default-transpiler path.
        Assert.True(missingName.Failed);
        Assert.Contains("viewUrl", missingName.Combined, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(server.Requests);
    }

    [Fact(DisplayName = "urls-list: listUrls")]
    public async Task ListUrlsReportsMissingFileAndSortedEntriesForEveryAlias()
    {
        var cli = new CliUnderTest();
        using var emptySandbox = Sandbox.Create(cli);

        var missing = await cli.Run(["-listUrls"], emptySandbox.ProjectPath, emptySandbox);

        Assert.Equal(0, missing.ExitCode);
        Assert.Contains(
            "No tool URLs configured. The ~/.effortless/tool_urls.json file does not exist.",
            missing.Stdout,
            StringComparison.Ordinal);
        Assert.Contains(
            "Use 'effortless setUrl toolname=url' to configure tool URLs.",
            missing.Stdout,
            StringComparison.Ordinal);

        var aliases = Behavior.IsLegacy
            ? new[] { "-listUrls", "listurls", "-lu", "-lt" }
            : new[] { "-listUrls", "listurls", "-lu" };
        foreach (var alias in aliases)
        {
            using var sandbox = Sandbox.Create(cli);
            ToolUrlTestSupport.WriteToolUrls(
                sandbox,
                new Dictionary<string, string>
                {
                    ["z"] = "https://z",
                    ["a"] = "http://a",
                });

            var result = await cli.Run([alias], sandbox.ProjectPath, sandbox);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Configured tool URL Overrides:", result.Stdout, StringComparison.Ordinal);
            var aIndex = result.Stdout.IndexOf("  a: http://a", StringComparison.Ordinal);
            var zIndex = result.Stdout.IndexOf("  z: https://z", StringComparison.Ordinal);
            Assert.True(aIndex >= 0 && zIndex > aIndex, result.Stdout);
            Assert.Contains("Total: 2 tool(s) configured", result.Stdout, StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "urls-remove: removeUrl")]
    public async Task RemoveUrlDeletesOverridesAndRestoresManagedBridge()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        ToolUrlTestSupport.WriteToolUrls(
            sandbox,
            new Dictionary<string, string>
            {
                ["a"] = "http://x",
                ["cli-cloud-bridge"] = "http://localhost:9",
            });

        var removed = await cli.Run(["-removeUrl", "a"], sandbox.ProjectPath, sandbox);
        var unknown = await cli.Run(["removeurl", "nope"], sandbox.ProjectPath, sandbox);
        var bridge = await cli.Run(
            ["-ru", "cli-cloud-bridge"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, removed.ExitCode);
        Assert.Equal(0, unknown.ExitCode);
        Assert.Equal(0, bridge.ExitCode);
        Assert.Contains(
            "Tool 'a' URL has been removed from your user configuration.",
            removed.Stdout,
            StringComparison.Ordinal);
        Assert.Contains(
            "Error removing tool URL: Error removing tool URL: Tool 'nope' is not configured in your tool URLs.",
            unknown.Stdout,
            StringComparison.Ordinal);
        Assert.Contains(
            $"Tool 'cli-cloud-bridge' URL restored to built-in default: {ResolutionTestSupport.BootstrapBridgeUrl}",
            bridge.Stdout,
            StringComparison.Ordinal);

        var urls = ToolUrlTestSupport.ReadToolUrls(sandbox);
        Assert.Null(urls["a"]);
        Assert.Equal(
            ResolutionTestSupport.BootstrapBridgeUrl,
            urls["cli-cloud-bridge"]?.GetValue<string>());
    }

    [Fact(DisplayName = "tool-url-verbs-canonical: the canonical ToolUrl verbs work")]
    public async Task CanonicalToolUrlVerbsSetListViewAndRemove()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);

        var set = await cli.Run(
            ["-setToolUrl", "a=http://x"],
            sandbox.ProjectPath,
            sandbox);
        var list = await cli.Run(["-listToolUrls"], sandbox.ProjectPath, sandbox);
        var view = await cli.Run(["-viewToolUrl", "a"], sandbox.ProjectPath, sandbox);
        var remove = await cli.Run(
            ["-removeToolUrl", "a"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, set.ExitCode);
        Assert.Equal(0, list.ExitCode);
        Assert.Equal(0, view.ExitCode);
        Assert.Equal(0, remove.ExitCode);
        Assert.Contains(
            "Tool 'a' URL set to: http://x",
            set.Stdout,
            StringComparison.Ordinal);
        Assert.Contains("  a: http://x", list.Stdout, StringComparison.Ordinal);
        Assert.Contains(
            "Tool 'a' is configured with URL: http://x",
            view.Stdout,
            StringComparison.Ordinal);
        Assert.Contains(
            "Tool 'a' URL has been removed from your user configuration.",
            remove.Stdout,
            StringComparison.Ordinal);
        Assert.Null(ToolUrlTestSupport.ReadToolUrls(sandbox)["a"]);
    }

    [Fact(DisplayName = "tool-url-verbs-legacy-aliases: the old *Url flags still work")]
    public async Task LegacyUrlFlagsStillWorkAfterTheRename()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);

        // D16: *ToolUrl is canonical, but the old *Url spellings are kept as
        // hidden aliases so the rename does not break existing scripts.
        var set = await cli.Run(["-setUrl", "a=http://x"], sandbox.ProjectPath, sandbox);
        var list = await cli.Run(["-listUrls"], sandbox.ProjectPath, sandbox);
        var view = await cli.Run(["-viewUrl", "a"], sandbox.ProjectPath, sandbox);
        var remove = await cli.Run(["-removeUrl", "a"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, set.ExitCode);
        Assert.Equal(0, list.ExitCode);
        Assert.Equal(0, view.ExitCode);
        Assert.Equal(0, remove.ExitCode);
        Assert.Contains(
            "Tool 'a' URL set to: http://x",
            set.Stdout,
            StringComparison.Ordinal);
        Assert.Contains("  a: http://x", list.Stdout, StringComparison.Ordinal);
        Assert.Contains(
            "Tool 'a' is configured with URL: http://x",
            view.Stdout,
            StringComparison.Ordinal);
        Assert.Contains(
            "Tool 'a' URL has been removed from your user configuration.",
            remove.Stdout,
            StringComparison.Ordinal);
        Assert.Null(ToolUrlTestSupport.ReadToolUrls(sandbox)["a"]);
    }

    [Fact(DisplayName = "urls-bareword-rt-quirk: bareword rt removes a URL instead of refreshing")]
    public async Task BarewordRtRemovesUrlInsteadOfRefreshing()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        ToolUrlTestSupport.WriteToolUrls(
            sandbox,
            new Dictionary<string, string> { ["a"] = "http://x" });

        var result = await cli.Run(["rt", "a"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "Tool 'a' URL has been removed from your user configuration.",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CLOUD-BRIDGE CALL TRIGGERED",
            result.Stdout,
            StringComparison.Ordinal);
        Assert.Null(ToolUrlTestSupport.ReadToolUrls(sandbox)["a"]);
    }
}
