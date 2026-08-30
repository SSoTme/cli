using Effortless.Cli.E2E.Harness;

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
}