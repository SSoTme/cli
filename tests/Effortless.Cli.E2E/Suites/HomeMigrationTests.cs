using System.Text.Json;
using System.Text.Json.Nodes;
using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

/// <summary>
/// Step 10: the legacy <c>~/.ssotme</c> user state directory is copied to
/// <c>~/.effortless</c> on first run, the per-project <c>.ssotme</c> ledger
/// directory is renamed, and child processes carry the renamed env var.
/// </summary>
public sealed class HomeMigrationTests
{
    private const string MigratedLine =
        "Migrated ~/.ssotme to ~/.effortless (legacy directory left in place).";

    [Fact(DisplayName = "home-migrates-legacy-dir: a legacy ~/.ssotme is copied to ~/.effortless on first run")]
    public async Task LegacyHomeIsCopiedOnFirstRun()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        var files = new Dictionary<string, string>
        {
            [".ssotme/tool_urls.json"] = """{"a":"http://x"}""",
            [".ssotme/remote_tools/ssotme-tools.json"] = """{"transpilerVersions":{}}""",
            [".ssotme/remote_tools/cli_version"] = "1.2.3",
            [".ssotme/bridge_version_index"] = "7",
            [".ssotme/seed_cache/x/f.txt"] = "seed bytes",
        };
        foreach (var file in files)
        {
            sandbox.WriteHomeFile(file.Key, file.Value);
        }

        var result = await cli.Run(["-version"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(CliUnderTest.DisplayVersion + Environment.NewLine, result.Stdout);
        Assert.Contains(MigratedLine, result.Stderr, StringComparison.Ordinal);
        Assert.Equal("""{"a":"http://x"}""", ReadHome(sandbox, ".effortless/tool_urls.json"));
        Assert.Equal("""{"transpilerVersions":{}}""", ReadHome(sandbox, ".effortless/remote_tools/effortless-tools.json"));
        Assert.Equal("1.2.3", ReadHome(sandbox, ".effortless/remote_tools/cli_version"));
        Assert.Equal("7", ReadHome(sandbox, ".effortless/bridge_version_index"));
        Assert.Equal("seed bytes", ReadHome(sandbox, ".effortless/seed_cache/x/f.txt"));
        Assert.False(File.Exists(HomePath(sandbox, ".effortless/remote_tools/ssotme-tools.json")));
    }

    [Fact(DisplayName = "home-migration-renames-key-files: ssotme.key and ssotme.<runAs>.key become effortless.key and effortless.<runAs>.key")]
    public async Task KeyFilesAreRenamed()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        var defaultKey = KeyJson("acme", "k1");
        var bobKey = KeyJson("acme", "kb");
        sandbox.WriteHomeFile(".ssotme/ssotme.key", defaultKey);
        sandbox.WriteHomeFile(".ssotme/ssotme.bob.key", bobKey);

        var info = await cli.Run(["-info"], sandbox.ProjectPath, sandbox);
        var infoBob = await cli.Run(["-info", "-runAs", "bob"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, info.ExitCode);
        Assert.Equal(0, infoBob.ExitCode);
        Assert.Contains("  acme: k1", info.Stdout, StringComparison.Ordinal);
        Assert.Contains("  acme: kb", infoBob.Stdout, StringComparison.Ordinal);
        Assert.Contains("Effortless CLI Version", info.Stdout, StringComparison.Ordinal);
        Assert.Equal(defaultKey, ReadHome(sandbox, ".effortless/effortless.key"));
        Assert.Equal(bobKey, ReadHome(sandbox, ".effortless/effortless.bob.key"));
        Assert.Empty(Directory.GetFiles(HomePath(sandbox, ".effortless"), "ssotme*.key"));
    }

    [Fact(DisplayName = "home-migration-leaves-legacy-untouched-and-marks-it: the legacy directory keeps every byte and gains only the marker")]
    public async Task LegacyDirectoryIsLeftInPlaceWithMarker()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        var files = new Dictionary<string, string>
        {
            [".ssotme/ssotme.key"] = KeyJson("acme", "k1"),
            [".ssotme/tool_urls.json"] = """{"a":"http://x"}""",
            [".ssotme/remote_tools/ssotme-tools.json"] = """{"transpilerVersions":{}}""",
            [".ssotme/remote_tools/ssotme.json"] = """{"Name":"remote_tools"}""",
            [".ssotme/remote_tools/effortless.json"] = """{"Name":"remote_tools","ProjectTranspilers":[]}""",
        };
        foreach (var file in files)
        {
            sandbox.WriteHomeFile(file.Key, file.Value);
        }

        var result = await cli.Run(["-version"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        foreach (var file in files)
        {
            Assert.Equal(file.Value, ReadHome(sandbox, file.Key));
        }

        var legacyFiles = Directory.GetFiles(HomePath(sandbox, ".ssotme"), "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(HomePath(sandbox, ".ssotme"), path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[]
            {
                "MIGRATED-TO-EFFORTLESS",
                "remote_tools/effortless.json",
                "remote_tools/ssotme-tools.json",
                "remote_tools/ssotme.json",
                "ssotme.key",
                "tool_urls.json",
            },
            legacyFiles);
        var marker = ReadHome(sandbox, ".ssotme/MIGRATED-TO-EFFORTLESS");
        Assert.Contains(HomePath(sandbox, ".effortless"), marker, StringComparison.Ordinal);
        Assert.Contains(CliUnderTest.PackageVersion, marker, StringComparison.Ordinal);
        Assert.Matches(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}", marker);
        Assert.False(File.Exists(HomePath(sandbox, ".effortless/remote_tools/ssotme.json")));
        Assert.True(File.Exists(HomePath(sandbox, ".effortless/remote_tools/effortless.json")));
    }

    [Fact(DisplayName = "home-both-present-uses-effortless-only: when both directories exist ~/.effortless wins and ~/.ssotme is never read")]
    public async Task BothPresentUsesEffortlessOnly()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        const string legacyUrls = """{"a":"http://old","b":"http://legacy"}""";
        sandbox.WriteHomeFile(".effortless/tool_urls.json", """{"a":"http://new"}""");
        sandbox.WriteHomeFile(".ssotme/tool_urls.json", legacyUrls);

        var list = await cli.Run(["-listToolUrls"], sandbox.ProjectPath, sandbox);
        var set = await cli.Run(["-setToolUrl", "c=http://c"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, list.ExitCode);
        Assert.Equal(0, set.ExitCode);
        Assert.Contains("http://new", list.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("legacy", list.Combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Migrated", list.Combined + set.Combined, StringComparison.Ordinal);
        var urls = JsonNode.Parse(ReadHome(sandbox, ".effortless/tool_urls.json"))!.AsObject();
        Assert.Equal("http://new", urls["a"]?.GetValue<string>());
        Assert.Equal("http://c", urls["c"]?.GetValue<string>());
        Assert.Null(urls["b"]);
        Assert.Equal(legacyUrls, ReadHome(sandbox, ".ssotme/tool_urls.json"));
        Assert.False(File.Exists(HomePath(sandbox, ".ssotme/MIGRATED-TO-EFFORTLESS")));
    }

    [Fact(DisplayName = "home-runas-key-mapping: a migrated -runAs key is used for account injection")]
    public async Task MigratedRunAsKeyIsUsedForAccountInjection()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(cli, server);
        sandbox.WriteFile("in.txt", "input");
        sandbox.WriteHomeFile(".effortless/effortless.key", KeyJson("acme", "default-key"));
        sandbox.WriteHomeFile(".effortless/effortless.bob.key", KeyJson("acme", "kb"));
        DemoteHomeToLegacy(sandbox);
        server.Enqueue(
            "echo",
            ToolBehavior.Files(FileSetEntry.TextFile("success.txt", "success", alwaysOverwrite: true)));

        var result = await cli.Run(
            ["echo", "-i", "in.txt", "-a", "acme", "-runAs", "bob"],
            sandbox.ProjectPath,
            sandbox);
        var request = await server.WaitForRequestAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(MigratedLine, result.Stderr, StringComparison.Ordinal);
        Assert.Contains("apiKey=kb", request.CliParams);
        Assert.DoesNotContain("apiKey=default-key", request.CliParams);
        Assert.True(File.Exists(HomePath(sandbox, ".effortless/effortless.bob.key")));
        server.ThrowIfFaulted();
    }

    [Fact(DisplayName = "home-migration-failure-is-fatal: a migration that cannot complete stops the CLI with a clear message and leaves no half copy")]
    public async Task MigrationFailureIsFatal()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        sandbox.WriteHomeFile(".ssotme/tool_urls.json", """{"a":"http://x"}""");
        // A regular file where the directory must land makes the final rename fail.
        File.WriteAllText(HomePath(sandbox, ".effortless"), "not a directory");

        var result = await cli.Run(["-version"], sandbox.ProjectPath, sandbox);

        Assert.True(result.Failed);
        Assert.Contains("Could not migrate ~/.ssotme to ~/.effortless:", result.Combined, StringComparison.Ordinal);
        Assert.Contains("nothing was changed", result.Combined, StringComparison.Ordinal);
        Assert.DoesNotContain(CliUnderTest.PackageVersion + Environment.NewLine, result.Stdout, StringComparison.Ordinal);
        Assert.Empty(Directory.GetDirectories(sandbox.HomePath, ".effortless.migrating-*"));
        Assert.False(File.Exists(HomePath(sandbox, ".ssotme/MIGRATED-TO-EFFORTLESS")));
        Assert.Equal("not a directory", ReadHome(sandbox, ".effortless"));
    }

    [Fact(DisplayName = "project-ledger-dir-rename: <root>/.ssotme is renamed to <root>/.effortless on project load")]
    public async Task ProjectLedgerDirectoryIsRenamed()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("Root", "", "to-uppercase"));
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(FileSetEntry.TextFile("root.txt", "root", alwaysOverwrite: true)));
        var first = await cli.Run(["build"], sandbox.ProjectPath, sandbox);
        Assert.Equal(0, first.ExitCode);
        var ledger = Assert.Single(WorkflowTestSupport.ZfsFiles(sandbox));
        var ledgerName = Path.GetFileName(ledger);
        Directory.Move(
            Path.Combine(sandbox.ProjectPath, ".effortless"),
            Path.Combine(sandbox.ProjectPath, ".ssotme"));
        Assert.False(Directory.Exists(Path.Combine(sandbox.ProjectPath, ".effortless")));

        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(FileSetEntry.TextFile("root.txt", "root again", alwaysOverwrite: true)));
        var second = await cli.Run(["build"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, second.ExitCode);
        Assert.False(Directory.Exists(Path.Combine(sandbox.ProjectPath, ".ssotme")));
        Assert.True(Directory.Exists(Path.Combine(sandbox.ProjectPath, ".effortless")));
        Assert.Equal(ledgerName, Path.GetFileName(Assert.Single(WorkflowTestSupport.ZfsFiles(sandbox))));
        Assert.Equal("root again", sandbox.ReadFile("root.txt"));

        var clean = await cli.Run(["clean"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, clean.ExitCode);
        Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, "root.txt")));
        Assert.False(Directory.Exists(Path.Combine(sandbox.ProjectPath, ".ssotme")));
        Assert.Equal(2, server.Requests.Count);
    }

    [Fact(DisplayName = "child-env-var-renamed: child CLI processes are spawned with EFFORTLESS_CHILD_PROCESS=1")]
    [Trait("Slow", "true")]
    public async Task ChildProcessesCarryRenamedEnvVariable()
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
            new WorkflowStep("Env", "", "-execute \"node env.js\""));
        File.WriteAllText(
            Path.Combine(nested, "env.js"),
            "require('fs').writeFileSync('env.txt', JSON.stringify({ e: process.env.EFFORTLESS_CHILD_PROCESS ?? null, s: process.env.SSOTME_CHILD_PROCESS ?? null }));\n");
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(FileSetEntry.TextFile("root.txt", "root", alwaysOverwrite: true)));

        var result = await cli.Run(
            ["buildWithSubprojects"],
            sandbox.ProjectPath,
            sandbox,
            timeoutMs: 180_000);

        Assert.Equal(0, result.ExitCode);
        var env = JsonNode.Parse(File.ReadAllText(Path.Combine(nested, "env.txt")))!.AsObject();
        Assert.Equal("1", env["e"]?.GetValue<string>());
        Assert.Null(env["s"]);
    }

    [Fact(DisplayName = "home-legacy-catalog-migrated-offline-build: a fresh catalog under ~/.ssotme lets a build run after migration without touching the bridge")]
    public async Task LegacyCatalogIsUsedAfterMigrationWithoutBridge()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        using var sandbox = WorkflowTestSupport.CreateProjectSandbox(
            cli,
            server,
            new WorkflowStep("Root", "", "to-uppercase"));
        DemoteHomeToLegacy(sandbox);
        server.Enqueue(
            "to-uppercase",
            ToolBehavior.Files(FileSetEntry.TextFile("root.txt", "root", alwaysOverwrite: true)));

        var result = await cli.Run(["build"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(MigratedLine, result.Stderr, StringComparison.Ordinal);
        Assert.Equal("root", sandbox.ReadFile("root.txt"));
        Assert.Equal("to-uppercase", Assert.Single(server.Requests).ToolName);
        Assert.DoesNotContain("CLOUD-BRIDGE CALL TRIGGERED", result.Stdout, StringComparison.Ordinal);
        Assert.True(File.Exists(HomePath(sandbox, ".effortless/remote_tools/effortless-tools.json")));
    }

    /// <summary>
    /// Turns a seeded v2 home into the legacy layout so the migration path is
    /// exercised: ~/.effortless -> ~/.ssotme with the v1 file names.
    /// </summary>
    private static void DemoteHomeToLegacy(Sandbox sandbox)
    {
        var effortless = HomePath(sandbox, ".effortless");
        var legacy = HomePath(sandbox, ".ssotme");
        Directory.Move(effortless, legacy);
        MoveIfExists(Path.Combine(legacy, "effortless.key"), Path.Combine(legacy, "ssotme.key"));
        foreach (var named in Directory.GetFiles(legacy, "effortless.*.key"))
        {
            MoveIfExists(named, Path.Combine(legacy, "ssotme." + Path.GetFileName(named)["effortless.".Length..]));
        }

        MoveIfExists(
            Path.Combine(legacy, "remote_tools", "effortless-tools.json"),
            Path.Combine(legacy, "remote_tools", "ssotme-tools.json"));
    }

    private static void MoveIfExists(string source, string destination)
    {
        if (File.Exists(source))
        {
            File.Move(source, destination);
        }
    }

    private static string HomePath(Sandbox sandbox, string relativePath) =>
        Path.Combine(sandbox.HomePath, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string ReadHome(Sandbox sandbox, string relativePath) =>
        File.ReadAllText(HomePath(sandbox, relativePath));

    private static string KeyJson(string account, string value) =>
        JsonSerializer.Serialize(
            new
            {
                EmailAddress = "test@example.invalid",
                Secret = "not-used",
                APIKeys = new Dictionary<string, string> { [account] = value },
            },
            new JsonSerializerOptions { WriteIndented = true });
}
