using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

/// <summary>
/// -checkVersion's interactive Yes/No/Always/Never prompt, run against the
/// real CLI process (mock GitHub for the commit check, a fake `npm` on PATH
/// standing in for the real reinstall).
/// </summary>
public sealed class UpdateCheckPromptTests
{
    private const string NewSha = "2222222222222222222222222222222222222222";

    /// <summary>
    /// Pre-dates the once-daily background check's gate to "already checked
    /// today" (CliUnderTest.TestUtcNow, the fixed date every sandboxed CLI
    /// process runs under) so it deterministically no-ops during these
    /// tests, isolating -checkVersion's own explicit prompt behavior from
    /// the unrelated Program.Main background check that runs in the same
    /// process on every command.
    /// </summary>
    private static void SeedAlreadyCheckedToday(Sandbox sandbox, string? preference = null)
    {
        var today = CliUnderTest.TestUtcNow.ToString("yyyy-MM-dd");
        var preferenceJson = preference is null ? "null" : $"\"{preference}\"";
        sandbox.WriteHomeFile(
            ".effortless/update_check.json",
            $$"""{"Preference":{{preferenceJson}},"LastCheckedDate":"{{today}}","PendingNotice":null}""");
    }

    [Theory(DisplayName = "check-version-prompt-flow: checkVersion prompts Yes/No/Always/Never on a changed commit")]
    [InlineData("y", true, null)]
    [InlineData("n", false, null)]
    [InlineData("a", true, "always")]
    [InlineData("e", false, "never")]
    public async Task PromptResponsesInvokeNpmAndPersistTheExpectedPreference(
        string response,
        bool expectNpmInstall,
        string? expectedPreference)
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        sandbox.SeedEmptyHome();
        SeedAlreadyCheckedToday(sandbox);
        sandbox.WriteFakeNpm("npm.log");
        await using var github = new MockGitHubServer();
        github.CommitsMainSha = NewSha;

        var result = await cli.Run(
            ["-checkVersion"],
            sandbox.ProjectPath,
            sandbox,
            stdin: $"{response}{Environment.NewLine}",
            environment: github.Environment);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Checking GitHub for the latest commit on main...", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Reinstall now? (Y)es / (N)o / (A)lways / n(E)ver:", result.Stdout, StringComparison.Ordinal);

        var npmLogPath = Path.Combine(sandbox.ProjectPath, "npm.log");
        Assert.Equal(expectNpmInstall, File.Exists(npmLogPath));
        if (expectNpmInstall)
        {
            Assert.Contains("install -g @effortlessapi/cli@latest", File.ReadAllText(npmLogPath), StringComparison.Ordinal);
            Assert.Contains("Reinstalled.", result.Stdout, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains(
                expectedPreference == "never"
                    ? "Never: future background checks will not act until you run -checkVersion again."
                    : "Skipped. Run -checkVersion again any time.",
                result.Stdout,
                StringComparison.Ordinal);
        }

        var statePath = Path.Combine(sandbox.HomePath, ".effortless", "update_check.json");
        if (expectedPreference is null)
        {
            Assert.False(File.Exists(statePath) && File.ReadAllText(statePath).Contains("\"Preference\":\"always\"", StringComparison.Ordinal));
            Assert.False(File.Exists(statePath) && File.ReadAllText(statePath).Contains("\"Preference\":\"never\"", StringComparison.Ordinal));
        }
        else
        {
            Assert.Contains($"\"Preference\": \"{expectedPreference}\"", File.ReadAllText(statePath), StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "check-version-unchanged: checkVersion reports up to date without prompting")]
    public async Task MatchingCommitShaReportsUpToDateWithoutPrompting()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        sandbox.SeedEmptyHome();
        SeedAlreadyCheckedToday(sandbox);
        await using var github = new MockGitHubServer();
        github.CommitsMainSha = CliVersionCommitSha();

        var result = await cli.Run(
            ["-checkVersion"],
            sandbox.ProjectPath,
            sandbox,
            environment: github.Environment);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Already on the latest version", result.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("Reinstall now?", result.Stdout, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "check-version-always-reprompts: explicit checkVersion always asks even when Always/Never is stored")]
    [InlineData("always")]
    [InlineData("never")]
    public async Task ExplicitRunAlwaysPromptsEvenWithAStoredPreference(string storedPreference)
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        sandbox.SeedEmptyHome();
        SeedAlreadyCheckedToday(sandbox, storedPreference);
        sandbox.WriteFakeNpm("npm.log");
        await using var github = new MockGitHubServer();
        github.CommitsMainSha = NewSha;

        var result = await cli.Run(
            ["-checkVersion"],
            sandbox.ProjectPath,
            sandbox,
            stdin: $"n{Environment.NewLine}",
            environment: github.Environment);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Reinstall now? (Y)es / (N)o / (A)lways / n(E)ver:", result.Stdout, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(sandbox.ProjectPath, "npm.log")));
        Assert.Contains(
            $"\"Preference\":\"{storedPreference}\"",
            File.ReadAllText(Path.Combine(sandbox.HomePath, ".effortless", "update_check.json")),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The CLI under test's own CliVersion.CommitSha. This black-box harness
    /// has no in-proc reference to Effortless.Cli.Core, so the value is read
    /// straight out of the source file the DLL under test was compiled from
    /// (the same source-of-truth CliUnderTest.PackageVersion/DisplayVersion
    /// already read from package.json).
    /// </summary>
    private static string CliVersionCommitSha()
    {
        var text = File.ReadAllText(
            Path.Combine(CliUnderTest.Root, "src", "Effortless.Cli.Core", "CliVersion.cs"));
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            "public const string CommitSha = \"(.*?)\";");
        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidDataException("CliVersion.cs has no CommitSha constant.");
    }
}
