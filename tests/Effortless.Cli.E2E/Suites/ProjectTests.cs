using System.Text.Json.Nodes;
using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

public sealed class ProjectTests
{
    private readonly CliUnderTest _cli = new();

    [Fact(DisplayName = "proj-init-empty: init scaffolds a project")]
    public async Task InitScaffoldsAProject()
    {
        using var sandbox = Sandbox.Create(_cli);
        var projectPath = CreateDirectory(sandbox.ProjectPath, "demo");

        var result = await _cli.Run(["-init"], projectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Added Setting: project-name: 'demo'", result.Stdout);
        var project = ReadObject(Path.Combine(projectPath, "effortless.json"));
        Assert.Equal("demo", String(project, "Name"));
        Assert.Single(Array(project, "ProjectSettings"));
        Assert.Equal("demo", SettingValue(project, "project-name"));
        Assert.Empty(Array(project, "ProjectTranspilers"));
        Assert.True(Guid.TryParse(String(project, "SSoTmeProjectId"), out _));

        Assert.Equal(StandardGitIgnore, File.ReadAllText(Path.Combine(projectPath, ".gitignore")));
        var environmentTemplate = File.ReadAllText(Path.Combine(projectPath, "effortless.env"));
        Assert.Contains("# AIRTABLE_PAT=xyz", environmentTemplate);
        Assert.Contains("effortless airtable-to-rulebook -account airtable", environmentTemplate);

        var rulebook = ReadObject(
            Path.Combine(projectPath, "effortless-rulebook", "effortless-rulebook.json"));
        Assert.Equal("demo", rulebook["project"]?["name"]?.GetValue<string>());
    }

    [Fact(DisplayName = "proj-init-name: init -name overrides the name")]
    public async Task InitNameOverridesTheDirectoryName()
    {
        using var sandbox = Sandbox.Create(_cli);
        var projectPath = CreateDirectory(sandbox.ProjectPath, "not-foo");

        var result = await _cli.Run(["-init", "-name", "Foo"], projectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        var project = ReadObject(Path.Combine(projectPath, "effortless.json"));
        Assert.Equal("Foo", String(project, "Name"));
        Assert.Equal("Foo", SettingValue(project, "project-name"));
    }

    [Fact(DisplayName = "proj-init-bareword: bareword init scaffolds the same project")]
    public async Task BarewordInitScaffoldsAProject()
    {
        using var sandbox = Sandbox.Create(_cli);
        var projectPath = CreateDirectory(sandbox.ProjectPath, "bareword-demo");

        var result = await _cli.Run(["init"], projectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        var project = ReadObject(Path.Combine(projectPath, "effortless.json"));
        Assert.Equal("bareword-demo", String(project, "Name"));
        Assert.Equal("bareword-demo", SettingValue(project, "project-name"));
        Assert.Empty(Array(project, "ProjectTranspilers"));
        Assert.True(File.Exists(Path.Combine(projectPath, ".gitignore")));
        Assert.True(File.Exists(Path.Combine(projectPath, "effortless.env")));
        Assert.True(
            File.Exists(
                Path.Combine(projectPath, "effortless-rulebook", "effortless-rulebook.json")));
    }

    [Fact(DisplayName = "proj-init-twice: legacy repeated init exits zero silently and canonicalizes JSON")]
    public async Task InitTwicePinsLegacySilentZeroExitAndJsonCanonicalization()
    {
        using var sandbox = Sandbox.Create(_cli);
        var projectPath = CreateDirectory(sandbox.ProjectPath, "twice");
        var first = await _cli.Run(["-init"], projectPath, sandbox);
        Assert.Equal(0, first.ExitCode);
        var projectFile = Path.Combine(projectPath, "effortless.json");
        var before = ReadObject(projectFile);

        var second = await _cli.Run(["-init"], projectPath, sandbox);

        // The legacy top-level exception handler swallows this failure and exits zero;
        // loading also serializes root CurrentPath from null to an empty string.
        Assert.Equal(0, second.ExitCode);
        Assert.Empty(second.Combined);
        var after = ReadObject(projectFile);
        Assert.True(before.ContainsKey("CurrentPath"));
        Assert.Null(before["CurrentPath"]);
        Assert.Equal(string.Empty, after["CurrentPath"]?.GetValue<string>());
        var beforeProjectName = FindSetting(before, "project-name")!;
        var afterProjectName = FindSetting(after, "project-name")!;
        Assert.True(Guid.TryParse(String(beforeProjectName, "ProjectSettingId"), out _));
        Assert.False(afterProjectName.ContainsKey("ProjectSettingId"));
        before.Remove("CurrentPath");
        after.Remove("CurrentPath");
        beforeProjectName.Remove("ProjectSettingId");
        Assert.True(
            JsonNode.DeepEquals(before, after),
            $"Unexpected repeated-init rewrite.{Environment.NewLine}Before: {before}{Environment.NewLine}After: {after}");
    }

    [Fact(DisplayName = "proj-init-rename: legacy init -name truncates a spaced name")]
    public async Task InitNamePinsLegacyTruncationOfSpacedName()
    {
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedProject("project-basic");

        var result = await _cli.Run(
            ["-init", "-name", "New Name"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        var project = Assert.IsType<JsonObject>(sandbox.ProjectFile);
        Assert.Equal("New", String(project, "Name"));
        Assert.Equal("New", SettingValue(project, "project-name"));
    }

    [Fact(DisplayName = "proj-init-force-subproject: native argv force creates a nested project")]
    public async Task InitForceCreatesNestedProjectWithoutChangingParent()
    {
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedProject("project-basic");
        var parentPath = Path.Combine(sandbox.ProjectPath, "effortless.json");
        var parentBefore = File.ReadAllBytes(parentPath);
        var subPath = CreateDirectory(sandbox.ProjectPath, "sub");

        var result = await _cli.Run(["-init", "force"], subPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(parentBefore, File.ReadAllBytes(parentPath));
        var child = ReadObject(Path.Combine(subPath, "effortless.json"));
        Assert.Equal("sub", String(child, "Name"));
        Assert.Equal("sub", SettingValue(child, "project-name"));
    }

    [Fact(DisplayName = "proj-init-gitignore-append: init appends effortless.env")]
    public async Task InitAppendsEnvironmentFileToExistingGitIgnore()
    {
        using var sandbox = Sandbox.Create(_cli);
        var projectPath = CreateDirectory(sandbox.ProjectPath, "gitignore-demo");
        File.WriteAllText(Path.Combine(projectPath, ".gitignore"), "existing-rule");

        var result = await _cli.Run(["-init"], projectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            $"existing-rule{Environment.NewLine}effortless.env{Environment.NewLine}",
            File.ReadAllText(Path.Combine(projectPath, ".gitignore")));
    }

    [Fact(DisplayName = "proj-walk-up: project commands walk up from nested directories")]
    public async Task ProjectCommandsWalkUpFromNestedDirectories()
    {
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedProject("project-basic");
        var nestedPath = CreateDirectory(sandbox.ProjectPath, "a", "b");

        var result = await _cli.Run(["-describe"], nestedPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"======    {CliReportedPath(sandbox.ProjectPath)}", result.Stdout);
        Assert.Contains("======  Basic Project", result.Stdout);
    }

    [Fact(DisplayName = "proj-migrate-ssotme-json: legacy ssotme files are renamed")]
    public async Task SsotmeProjectAndEnvironmentFilesAreRenamed()
    {
        using var sandbox = Sandbox.Create(_cli);
        var projectJson = MinimalProjectJson("Migrated Project");
        const string environment = "LEGACY_KEY=value\n";
        sandbox.WriteFile("ssotme.json", projectJson);
        sandbox.WriteFile("ssotme.env", environment);

        var result = await _cli.Run(["-describe"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, "ssotme.json")));
        Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, "ssotme.env")));
        Assert.Equal(projectJson, sandbox.ReadFile("effortless.json"));
        Assert.Equal(environment, sandbox.ReadFile("effortless.env"));
    }

    [Fact(DisplayName = "proj-legacy-names: aicapture.json and SSoTmeProject.json still load")]
    public async Task OlderProjectFileNamesLoadAndSaveInPlace()
    {
        foreach (var fileName in new[] { "aicapture.json", "SSoTmeProject.json" })
        {
            using var sandbox = Sandbox.Create(_cli);
            sandbox.WriteFile(fileName, MinimalProjectJson($"Legacy {fileName}"));

            var describe = await _cli.Run(["-describe"], sandbox.ProjectPath, sandbox);
            var save = await _cli.Run(
                ["-addSetting", "legacy=yes"],
                sandbox.ProjectPath,
                sandbox);

            Assert.Equal(0, describe.ExitCode);
            Assert.Equal(0, save.ExitCode);
            Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, "effortless.json")));
            Assert.Equal("yes", SettingValue(ReadObject(Path.Combine(sandbox.ProjectPath, fileName)), "legacy"));
        }
    }

    [Fact(DisplayName = "proj-generate-id: loading generates and saves a missing project id")]
    public async Task LoadingGeneratesAndSavesMissingProjectId()
    {
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedProject("project-no-id");
        var before = Assert.IsType<JsonObject>(sandbox.ProjectFile);
        Assert.False(before.ContainsKey("SSoTmeProjectId"));

        var result = await _cli.Run(["-describe"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        var after = Assert.IsType<JsonObject>(sandbox.ProjectFile);
        Assert.True(Guid.TryParse(String(after, "SSoTmeProjectId"), out _));
        Assert.Equal("No Id Project", String(after, "Name"));
        Assert.Equal("kept", SettingValue(after, "custom"));
        Assert.Equal("No Id Project", SettingValue(after, "project-name"));
        Assert.Empty(Array(after, "ProjectTranspilers"));
        Assert.False(after["ShowHidden"]?.GetValue<bool>());
        Assert.False(after["ShowAllFiles"]?.GetValue<bool>());
        Assert.True(after.ContainsKey("CurrentPath"));
        Assert.Null(after["CurrentPath"]);
        Assert.True(after.ContainsKey("SSoTmeProjectFiles"));
        Assert.Null(after["SSoTmeProjectFiles"]);
    }

    [Fact(DisplayName = "proj-describe: describe and list print the project summary")]
    public async Task DescribeAndListPrintProjectSummary()
    {
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedProject("project-describe");

        var describe = await _cli.Run(["-describe"], sandbox.ProjectPath, sandbox);
        var list = await _cli.Run(["list"], sandbox.ProjectPath, sandbox);

        AssertProjectSummary(describe, sandbox, includeRoot: true, includeSub: true);
        AssertProjectSummary(list, sandbox, includeRoot: true, includeSub: true);
    }

    [Fact(DisplayName = "proj-describe-subtree: describe filters transpilers by cwd subtree")]
    public async Task DescribeFiltersTranspilersByCurrentSubtree()
    {
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedProject("project-describe");
        var subPath = CreateDirectory(sandbox.ProjectPath, "sub");

        var result = await _cli.Run(["-describe"], subPath, sandbox);

        AssertProjectSummary(result, sandbox, includeRoot: false, includeSub: true);
    }

    [Fact(DisplayName = "proj-describe-all: describeAll and da ignore cwd filtering")]
    public async Task DescribeAllIgnoresCurrentSubtree()
    {
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedProject("project-describe");
        var subPath = CreateDirectory(sandbox.ProjectPath, "sub");

        var describeAll = await _cli.Run(["-describeAll"], subPath, sandbox);
        var da = await _cli.Run(["da"], subPath, sandbox);

        AssertProjectSummary(describeAll, sandbox, includeRoot: true, includeSub: true);
        AssertProjectSummary(da, sandbox, includeRoot: true, includeSub: true);
    }

    [Fact(DisplayName = "proj-no-project-silent: legacy describe outside a project prints an error")]
    public async Task DescribeOutsideAProjectPinsLegacyErrorOutput()
    {
        using var sandbox = Sandbox.Create(_cli);

        var result = await _cli.Run(["-describe"], sandbox.ProjectPath, sandbox);

        Assert.True(result.Failed);
        Assert.Equal(
            """
            ERROR: No project found in this directory or any parent directory.

            Run `effortless -init` to create a new project in this directory.
            """,
            NormalizeNewlines(result.Combined).Trim());
    }

    [Fact(DisplayName = "proj-list-settings: listSettings and ls print project settings")]
    public async Task ListSettingsAndAliasPrintProjectSettings()
    {
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedProject("project-settings");

        var longName = await _cli.Run(["-listSettings"], sandbox.ProjectPath, sandbox);
        var alias = await _cli.Run(["-ls"], sandbox.ProjectPath, sandbox);

        foreach (var result in new[] { longName, alias })
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("SETTINGS: ", result.Stdout);
            Assert.Contains("    - project-name = Settings Project", result.Stdout);
            Assert.Contains("    - a = 1", result.Stdout);
        }
    }

    [Fact(DisplayName = "proj-add-setting: addSetting adds and case-insensitively replaces")]
    public async Task AddSettingAddsAndCaseInsensitivelyReplaces()
    {
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedProject("project-basic");

        var add = await _cli.Run(["-addSetting", "a=1"], sandbox.ProjectPath, sandbox);
        var replace = await _cli.Run(["-as", "A=2"], sandbox.ProjectPath, sandbox);
        var equals = await _cli.Run(["-as", "b=x=y"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, add.ExitCode);
        Assert.Equal(0, replace.ExitCode);
        Assert.Equal(0, equals.ExitCode);
        Assert.Contains("Added Setting: a: '1'", add.Stdout);

        var project = Assert.IsType<JsonObject>(sandbox.ProjectFile);
        var matchingA = Settings(project)
            .Where(setting => String(setting, "Name").Equals("a", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Single(matchingA);
        Assert.Equal("2", String(matchingA[0], "Value"));

        // Legacy Split/Join behavior removes every '=' after the first one.
        Assert.Equal("xy", SettingValue(project, "b"));
    }

    [Fact(DisplayName = "proj-add-setting-bad: legacy accepts =x as an empty-valued x setting")]
    public async Task AddSettingWithMissingNamePinsLegacyParsing()
    {
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedProject("project-basic");

        var result = await _cli.Run(["-addSetting", "=x"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Added Setting: x: ''", result.Stdout);
        Assert.Equal(string.Empty, SettingValue(Assert.IsType<JsonObject>(sandbox.ProjectFile), "x"));
        Assert.DoesNotContain("Settings must be in the format of 'name=value'", result.Combined);
    }

    [Fact(DisplayName = "proj-add-setting-project-name: project-name setting renames project")]
    public async Task ProjectNameSettingRenamesProject()
    {
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedProject("project-basic");

        var result = await _cli.Run(
            ["-as", "project-name=Zed"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        var project = Assert.IsType<JsonObject>(sandbox.ProjectFile);
        Assert.Equal("Zed", String(project, "Name"));
        Assert.Equal("Zed", SettingValue(project, "project-name"));
    }

    [Fact(DisplayName = "proj-remove-setting: removeSetting removes and missing fails")]
    public async Task RemoveSettingRemovesAndMissingSettingFails()
    {
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedProject("project-settings");

        var remove = await _cli.Run(["-removeSetting", "a"], sandbox.ProjectPath, sandbox);
        var missing = await _cli.Run(["-rs", "missing"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, remove.ExitCode);
        Assert.Contains("Successfully Removed Setting: a: '1'", remove.Stdout);
        Assert.Null(FindSetting(Assert.IsType<JsonObject>(sandbox.ProjectFile), "a"));
        Assert.True(missing.Failed);
        Assert.Contains("Can't find matching setting: missing", missing.Combined);
    }

    [Fact(DisplayName = "proj-save-preserves-custom: save preserves custom transpiler properties")]
    public async Task SavePreservesCustomTranspilerProperties()
    {
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedProject("project-save-custom");

        var result = await _cli.Run(["-as", "x=1"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        var project = Assert.IsType<JsonObject>(sandbox.ProjectFile);
        var step = Assert.IsType<JsonObject>(Assert.Single(Array(project, "ProjectTranspilers")));
        Assert.Equal("hand written", String(step, "Description"));
        Assert.True(step["Enabled"]?.GetValue<bool>());
    }

    [Fact(DisplayName = "proj-save-no-resurrect: upgrade does not resurrect PinnedVersion")]
    public async Task UpgradeDoesNotResurrectPinnedVersion()
    {
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server);
        server.IndexJson = index.Json;
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedHome(index);
        sandbox.SeedProject("project-pinned");

        var result = await _cli.Run(
            ["upgrade", "to-uppercase"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        var project = Assert.IsType<JsonObject>(sandbox.ProjectFile);
        var step = Assert.IsType<JsonObject>(Assert.Single(Array(project, "ProjectTranspilers")));
        Assert.False(step.ContainsKey("PinnedVersion"));
        Assert.Equal("v2026.01.01.0001", String(step, "LastVersionUsed"));
        Assert.Contains("unpinned", result.Stdout);
        server.ThrowIfFaulted();
    }

    [Fact(DisplayName = "proj-save-format: save emits the legacy project JSON format")]
    public async Task SaveEmitsLegacyProjectJsonFormat()
    {
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedProject("project-save-format");

        var result = await _cli.Run(["-as", "x=1"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        var contents = sandbox.ReadFile("effortless.json");
        Assert.EndsWith(Environment.NewLine, contents);
        Assert.Contains($"{Environment.NewLine}  \"Name\":", contents);
        Assert.Contains($"{Environment.NewLine}    {{", contents);

        var project = Assert.IsType<JsonObject>(sandbox.ProjectFile);
        Assert.False(project.ContainsKey("RootPath"));
        Assert.False(project.ContainsKey("ExpandedPaths"));
        Assert.False(project.ContainsKey("HiddenPaths"));
        Assert.False(project["ShowHidden"]?.GetValue<bool>());
        Assert.False(project["ShowAllFiles"]?.GetValue<bool>());
        Assert.True(project.ContainsKey("CurrentPath"));
        Assert.Null(project["CurrentPath"]);
        Assert.True(project.ContainsKey("SSoTmeProjectFiles"));
        Assert.Null(project["SSoTmeProjectFiles"]);

        foreach (var setting in Settings(project))
        {
            Assert.False(setting.ContainsKey("ProjectSettingId"));
        }

        foreach (var step in Array(project, "ProjectTranspilers").Select(Assert.IsType<JsonObject>))
        {
            Assert.False(step.ContainsKey("ProjectTranspilerId"));
            Assert.False(step.ContainsKey("SSoTmeProjectId"));
            Assert.True(step.ContainsKey("IsDisabled"));
            Assert.True(step.ContainsKey("IsSSoTTranspiler"));
        }
    }

    [Fact(DisplayName = "proj-save-order: replacing an installed step keeps its index")]
    public async Task ReplacingInstalledStepKeepsItsIndex()
    {
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server);
        server.IndexJson = index.Json;
        server.Enqueue("echo", ToolBehavior.Files());
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedHome(index);
        sandbox.SeedProject("project-order");

        var result = await _cli.Run(
            ["echo", "-install", "-p", "new=value"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        var project = Assert.IsType<JsonObject>(sandbox.ProjectFile);
        var commands = Array(project, "ProjectTranspilers")
            .Select(Assert.IsType<JsonObject>)
            .Select(step => String(step, "CommandLine"))
            .ToArray();
        Assert.Equal(3, commands.Length);
        Assert.StartsWith("to-uppercase ", commands[0]);
        Assert.StartsWith("echo ", commands[1]);
        Assert.Contains("new=value", commands[1]);
        Assert.StartsWith("third-tool ", commands[2]);
        Assert.Contains("Replaced Transpiler", result.Stdout);
        server.ThrowIfFaulted();
    }

    [Fact(DisplayName = "meta-debug-banner: -debug announces itself and the POST URL")]
    public async Task DebugAnnouncesItselfAndPostUrl()
    {
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server);
        server.IndexJson = index.Json;
        server.Enqueue("to-uppercase", ToolBehavior.Echo());
        using var sandbox = Sandbox.Create(_cli);
        sandbox.SeedHome(index);
        sandbox.SeedProject("project-debug");

        var result = await _cli.Run(
            ["to-uppercase", "-i", "in.txt", "-debug"],
            sandbox.ProjectPath,
            sandbox);
        var request = await server.WaitForRequestAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("DEBUG OUTPUT ENABLED", result.Stdout);
        Assert.Contains($"POST {server.ToolUri("to-uppercase")}", result.Stdout);
        Assert.True(request.CliDebug);
        Assert.Equal(
            sandbox.ReadFile("in.txt").ToUpperInvariant(),
            sandbox.ReadFile("Output.txt"));
        server.ThrowIfFaulted();
    }

    private static void AssertProjectSummary(
        CliResult result,
        Sandbox sandbox,
        bool includeRoot,
        bool includeSub)
    {
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("======  Describe Project", result.Stdout);
        Assert.Contains($"======    {CliReportedPath(sandbox.ProjectPath)}", result.Stdout);
        Assert.Contains("SETTINGS: ", result.Stdout);
        Assert.Contains("    - a = 1", result.Stdout);

        if (includeRoot)
        {
            Assert.Contains("---- Root Step", result.Stdout);
            Assert.Contains("Command Line:> effortless to-uppercase -i root.txt", result.Stdout);
        }
        else
        {
            Assert.DoesNotContain("---- Root Step", result.Stdout);
            Assert.DoesNotContain("to-uppercase -i root.txt", result.Stdout);
        }

        if (includeSub)
        {
            Assert.Contains("---- Sub Step    **** DISABLED ****", result.Stdout);
            Assert.Contains("Command Line:> effortless echo -i sub.txt", result.Stdout);
        }
        else
        {
            Assert.DoesNotContain("---- Sub Step", result.Stdout);
            Assert.DoesNotContain("echo -i sub.txt", result.Stdout);
        }
    }

    private static string CreateDirectory(string root, params string[] segments)
    {
        var path = segments.Aggregate(root, Path.Combine);
        Directory.CreateDirectory(path);
        return path;
    }

    private static JsonObject ReadObject(string path) =>
        Assert.IsType<JsonObject>(
            JsonNode.Parse(File.ReadAllText(path))
            ?? throw new InvalidDataException($"JSON file '{path}' contains null."));

    private static JsonArray Array(JsonObject value, string propertyName) =>
        Assert.IsType<JsonArray>(value[propertyName]);

    private static string String(JsonObject value, string propertyName) =>
        value[propertyName]?.GetValue<string>()
        ?? throw new InvalidDataException($"JSON property '{propertyName}' is missing or null.");

    private static IEnumerable<JsonObject> Settings(JsonObject project) =>
        Array(project, "ProjectSettings").Select(Assert.IsType<JsonObject>);

    private static JsonObject? FindSetting(JsonObject project, string name) =>
        Settings(project)
            .SingleOrDefault(
                setting => String(setting, "Name").Equals(name, StringComparison.OrdinalIgnoreCase));

    private static string SettingValue(JsonObject project, string name) =>
        String(
            FindSetting(project, name)
            ?? throw new Xunit.Sdk.XunitException($"Project setting '{name}' was not found."),
            "Value");

    private static string MinimalProjectJson(string name) =>
        $$"""
        {
          "Name": "{{name}}",
          "SSoTmeProjectId": "2f6f4055-4ec8-479e-aee8-07eea079a83f",
          "ProjectSettings": [
            {
              "Name": "project-name",
              "Value": "{{name}}"
            }
          ],
          "ProjectTranspilers": []
        }
        """;

    private static string CliReportedPath(string path) =>
        OperatingSystem.IsMacOS() && path.StartsWith("/var/", StringComparison.Ordinal)
            ? "/private" + path
            : path;

    private static string NormalizeNewlines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private const string StandardGitIgnore = """
        /**/obj/**/*
        /**/bin/**/*
        /**/.ssotme/**/*
        /**/DSPXml/**/*
        /SSoT/__patch.json
        /**/.vs/**/*
        /**/node_modules/**/*
        /**/.vscode/**/*
        ssotme.env
        effortless.env
        """;
}
