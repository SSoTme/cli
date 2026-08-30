using Effortless.Cli.E2E.Harness;
using System.Text;
using System.Text.Json;

namespace Effortless.Cli.E2E.Suites;

public sealed class MetaTests
{
    public static TheoryData<string> VersionForms =>
        new()
        {
            "-version",
            "-v",
            "version",
            "v",
        };

    [Theory(DisplayName = "meta-version-flag: version prints only the version")]
    [MemberData(nameof(VersionForms))]
    public async Task VersionPrintsOnlyPackageVersion(string argument)
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);

        var result = await cli.Run([argument], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(CliUnderTest.PackageVersion + Environment.NewLine, result.Stdout);
        Assert.Empty(result.Stderr);
    }

    [Theory(DisplayName = "meta-help: help lists every retained option")]
    [InlineData("-help")]
    [InlineData("-h")]
    [InlineData("help")]
    public async Task HelpListsEveryRetainedOption(string argument)
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);

        var result = await cli.Run([argument], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("SSoTme CLI", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Syntax: effortless [account/]transpiler [Options]", result.Stdout, StringComparison.Ordinal);
        foreach (var flag in RetainedFlags())
        {
            Assert.Contains(flag, result.Stdout, StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "meta-unknown-option: unknown option reports parser errors")]
    public async Task UnknownOptionReportsParserError()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        WriteMinimalProject(sandbox);

        var result = await cli.Run(["-notAnOption"], sandbox.ProjectPath, sandbox);

        Assert.True(result.Failed);
        Assert.Contains("notAnOption", result.Combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "meta-no-args-in-project: no arguments inside a project")]
    public async Task NoArgumentsInsideProjectReportsMissingTranspiler()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        WriteMinimalProject(sandbox);

        var result = await cli.Run([], sandbox.ProjectPath, sandbox);

        Assert.True(result.Failed);
        Assert.Contains("Missing argument name of transpiler", result.Combined, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "meta-no-args-outside-project: no arguments outside a project")]
    public async Task NoArgumentsOutsideProjectReportsMissingProject()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);

        var result = await cli.Run([], sandbox.ProjectPath, sandbox);

        Assert.True(result.Failed);
        Assert.Contains(
            "ERROR: No project found in this directory or any parent directory.",
            result.Combined,
            StringComparison.Ordinal);
        Assert.Contains("-init", result.Combined, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "meta-info-not-logged-in: info with no tokens")]
    [InlineData("-info")]
    [InlineData("info")]
    public async Task InfoShowsLoggedOutProjectConfiguration(string argument)
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        WriteMinimalProject(sandbox);

        var result = await cli.Run([argument], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"SSoTme CLI Version {CliUnderTest.PackageVersion}", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Project: characterization-project", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Logged in as: (not logged in)", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Subscription: n/a", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("No API keys configured.", result.Stdout, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "meta-info-with-keys: info lists API keys")]
    public async Task InfoListsConfiguredApiKeys()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        WriteMinimalProject(sandbox);
        WriteHomeFile(
            sandbox,
            "ssotme.key",
            """{"EmailAddress":"","Secret":"","APIKeys":{"airtable":"pat123"}}""");

        var result = await cli.Run(["-info"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Configured API keys:", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("  airtable: pat123", result.Stdout, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "meta-info-project-jwt: info prefers the project JWT")]
    public async Task InfoPrefersProjectJwt()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        WriteMinimalProject(sandbox);
        sandbox.WriteFile("effortless.env", $"EFFORTLESS_JWT={CreateJwt("a@b.c")}\n");
        var globalJwt = CreateJwt("x@y.z");
        WriteHomeFile(sandbox, "effortlessapi_token.txt", globalJwt);
        WriteHomeFile(
            sandbox,
            "effortlessapi_token_info.json",
            """{"Email":"x@y.z"}""");

        var result = await cli.Run(["-info"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Logged in as: a@b.c [project]", result.Stdout, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "meta-info-global-jwt: info falls back to the global JWT")]
    public async Task InfoUsesGlobalJwt()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        WriteMinimalProject(sandbox);
        var jwt = CreateJwt("x@y.z");
        WriteHomeFile(sandbox, "effortlessapi_token.txt", jwt);
        WriteHomeFile(
            sandbox,
            "effortlessapi_token_info.json",
            """{"Email":"x@y.z"}""");

        var result = await cli.Run(["-info"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Logged in as: x@y.z [global]", result.Stdout, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "meta-info-no-project: info outside a project")]
    public async Task InfoWorksOutsideProject()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);

        var result = await cli.Run(["-info"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Project: <null>", result.Stdout, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "meta-home-guard: harness never uses the real home")]
    public void HarnessNeverUsesRealHome()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);

        var realHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.NotEqual(
            Path.GetFullPath(realHome).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(sandbox.HomePath).TrimEnd(Path.DirectorySeparatorChar));
    }

    private static void WriteMinimalProject(Sandbox sandbox)
    {
        sandbox.WriteFile(
            "effortless.json",
            """
            {
              "Name": "characterization-project",
              "ProjectSettings": [],
              "ProjectTranspilers": []
            }
            """);
    }

    private static void WriteHomeFile(Sandbox sandbox, string relativePath, string contents)
    {
        var path = Path.Combine(sandbox.HomePath, ".ssotme", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private static string CreateJwt(string email)
    {
        var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var header = Base64Url("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64Url(JsonSerializer.Serialize(new { email, exp = expires }));
        return $"{header}.{payload}.";
    }

    private static string Base64Url(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static IEnumerable<string> RetainedFlags()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(CliUnderTest.Root, "effortless-rulebook", "effortless-rulebook.json")));
        var root = document.RootElement;
        var retained = root.GetProperty("Dispositions").GetProperty("data")
            .EnumerateArray()
            .ToDictionary(
                row => row.GetProperty("DispositionId").GetString()!,
                row => row.GetProperty("IsRetained").GetBoolean(),
                StringComparer.Ordinal);

        return root.GetProperty("CliOptions").GetProperty("data")
            .EnumerateArray()
            .Where(row =>
                retained[row.GetProperty("Disposition").GetString()!]
                && row.GetProperty("LegacyRefCount").GetInt32() > 0)
            .Select(row => row.GetProperty("Flag").GetString()!)
            .ToArray();
    }
}