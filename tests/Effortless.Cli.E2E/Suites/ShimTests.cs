using System.Security.Cryptography;
using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

[CollectionDefinition(ShimTestCollection.Name, DisableParallelization = true)]
public sealed class ShimTestCollection
{
    public const string Name = "e2e-shim serial";
}

[Collection(ShimTestCollection.Name)]
public sealed class ShimTests
{
    [Fact(DisplayName = "shim-argv-passthrough: cli.js passes arguments")]
    public async Task CliJsPinsLegacyArgumentJoiningAndVersionPassthrough()
    {
        var cli = new CliUnderTest();
        await using var server = new MockToolServer();
        var index = IndexFixture.Load(server);
        server.IndexJson = index.Json;
        server.Enqueue("echo", ToolBehavior.Echo());
        using var sandbox = Sandbox.Create(cli);
        sandbox.SeedHome(index);
        sandbox.SeedProject("transpile");
        sandbox.WriteFile("in.txt", "shim input");
        var shim = ShimUnderTest.CreatePrebuilt(cli, sandbox);
        var csprojBefore = File.ReadAllText(shim.CsprojPath);
        var versionConstantBefore =
            File.ReadAllText(shim.VersionConstantPath);

        var version = await shim.RunNode(["-version"], sandbox.ProjectPath);
        var transpile = await shim.RunNode(
            ["echo", "-i", "in.txt", "-p", "a=b c"],
            sandbox.ProjectPath);
        var request = await server.WaitForRequestAsync();

        Assert.Equal(0, version.ExitCode);
        Assert.Equal(
            CliUnderTest.PackageVersion + Environment.NewLine,
            version.Stdout);
        Assert.Equal(0, transpile.ExitCode);
        if (Behavior.IsLegacy)
        {
            Assert.Contains("a=b", request.CliParams);
            Assert.Contains("param1=c", request.CliParams);
            Assert.DoesNotContain("a=b c", request.CliParams);
        }
        else
        {
            Assert.Contains("a=b c", request.CliParams);
            Assert.DoesNotContain("param1=c", request.CliParams);
        }

        Assert.Equal(csprojBefore, File.ReadAllText(shim.CsprojPath));
        Assert.Equal(
            versionConstantBefore,
            File.ReadAllText(shim.VersionConstantPath));
        server.ThrowIfFaulted();
    }

    [Fact(DisplayName = "shim-exit-code: cli.js propagates the exit code")]
    public async Task CliJsPinsLegacyIgnoredChildExitCode()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        var shim = ShimUnderTest.CreatePrebuilt(cli, sandbox);

        var result = await shim.RunNode(["-describe"], sandbox.ProjectPath);

        Assert.Contains(
            "ERROR: No project found in this directory or any parent directory.",
            result.Combined,
            StringComparison.Ordinal);
        if (Behavior.IsLegacy)
        {
            Assert.Equal(0, result.ExitCode);
        }
        else
        {
            Assert.True(result.Failed);
        }
    }

    [Fact(DisplayName = "shim-version-sync: cli.js syncs the version")]
    [Trait("Slow", "true")]
    public async Task CliJsSynchronizesVersionInAnIsolatedSourceTree()
    {
        const string testVersion = "2099-12-31.23.59";
        const string expectedCsprojVersion = "2099.12.31.2359";
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        var sourcePaths = VersionSourcePaths();
        var sourceHashesBefore = sourcePaths.ToDictionary(path => path, HashFile);
        var shim = ShimUnderTest.CreateFullSource(cli, sandbox);
        shim.SetPackageVersion(testVersion);

        var result = await shim.RunNode(
            ["-version"],
            sandbox.ProjectPath,
            timeoutMs: 300_000);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Building .NET solution...", result.Stdout, StringComparison.Ordinal);
        Assert.Contains(testVersion + Environment.NewLine, result.Stdout, StringComparison.Ordinal);
        Assert.Contains(
            $"<Version>{expectedCsprojVersion}</Version>",
            File.ReadAllText(shim.CsprojPath),
            StringComparison.Ordinal);
        Assert.Contains(
            Behavior.IsLegacy
                ? $"public string CLI_VERSION = \"{testVersion}\";"
                : $"public const string Value = \"{testVersion}\";",
            File.ReadAllText(shim.VersionConstantPath),
            StringComparison.Ordinal);
        foreach (var path in sourcePaths)
        {
            Assert.Equal(sourceHashesBefore[path], HashFile(path));
        }
    }

    [Fact(DisplayName = "shim-aliases: all four bin names work")]
    public async Task AllNpmAliasesInvokeTheSameShim()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        var shim = ShimUnderTest.CreatePrebuilt(cli, sandbox);
        var aliases = shim.PackageBinNames();
        Assert.Equal(
            new[] { "aic", "aicapture", "effortless", "ssotme" },
            aliases);
        await shim.InstallAliases();

        var outputs = new List<string>();
        foreach (var alias in aliases)
        {
            var result = await shim.RunAlias(alias, ["-v"], sandbox.ProjectPath);
            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.Stderr);
            outputs.Add(result.Stdout);
        }

        Assert.All(
            outputs,
            output => Assert.Equal(CliUnderTest.PackageVersion + Environment.NewLine, output));
        Assert.Single(outputs.Distinct(StringComparer.Ordinal));
    }

    private static IReadOnlyList<string> VersionSourcePaths()
    {
        var paths = new List<string>
        {
            Path.Combine(CliUnderTest.Root, "package.json"),
            Path.Combine(CliUnderTest.Root, "cli.js"),
        };
        if (Behavior.IsLegacy)
        {
            paths.Add(
                Path.Combine(
                    CliUnderTest.Root,
                    "Windows",
                    "CLI",
                    "SSoTme.OST.CLI.csproj"));
            paths.Add(
                Path.Combine(
                    CliUnderTest.Root,
                    "Windows",
                    "Lib",
                    "CLIOptions",
                    "SSoTmeCLIHandler.cs"));
        }
        else
        {
            paths.Add(
                Path.Combine(
                    CliUnderTest.Root,
                    "src",
                    "Effortless.Cli",
                    "Effortless.Cli.csproj"));
            paths.Add(
                Path.Combine(
                    CliUnderTest.Root,
                    "src",
                    "Effortless.Cli.Core",
                    "CliVersion.cs"));
        }

        return paths;
    }

    private static string HashFile(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
