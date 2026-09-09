using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

/// <summary>
/// The once-daily background update check (Program.Main) and the
/// next-command pending-notice plumbing. The prompt-flow / unchanged /
/// always-reprompts scenarios for -checkVersion itself are unit-tested
/// directly against UpdateChecker (tests/Effortless.Cli.Tests/UpdateCheckerTests.cs)
/// since they need an injectable HttpClient and fake process launcher that
/// this black-box, out-of-process harness has no seam for.
///
/// Known limitation (flagged, not silently worked around): Program.Main's
/// background check is Task.Run-and-forget, never awaited, per spec. A
/// short-lived CLI process can and does exit before an awaited background
/// HTTP call / npm install completes, so this suite verifies what is
/// deterministically true regardless of that race — the once-per-day gate
/// bookkeeping and the pending-notice plumbing — rather than asserting a
/// real background reinstall always finishes within one process lifetime.
/// </summary>
public sealed class UpdateCheckBackgroundTests
{
    [Fact(DisplayName = "update-check-background-daily: background check runs at most once per calendar day and honors Always/Never without prompting")]
    public async Task BackgroundCheckGatesOncePerCalendarDayAndNeverBlocksTheForegroundCommand()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        sandbox.SeedEmptyHome();
        sandbox.WriteHomeFile(
            ".effortless/update_check.json",
            """{"Preference":"always","LastCheckedDate":"2020-01-01","PendingNotice":null}""");
        await using var github = new MockGitHubServer();
        github.CommitsMainSha = "changed-sha-not-matching-cli-version";

        var first = await cli.Run(
            ["-version"],
            sandbox.ProjectPath,
            sandbox,
            environment: github.Environment);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(CliUnderTest.DisplayVersion + Environment.NewLine, first.Stdout);

        // The gate advances LastCheckedDate synchronously (before the
        // fire-and-forget background work even starts), so a same-day rerun
        // must never touch GitHub again this calendar day.
        var stateAfterFirst = File.ReadAllText(
            Path.Combine(sandbox.HomePath, ".effortless", "update_check.json"));
        Assert.DoesNotContain("2020-01-01", stateAfterFirst, StringComparison.Ordinal);

        var second = await cli.Run(
            ["-version"],
            sandbox.ProjectPath,
            sandbox,
            environment: github.Environment);

        Assert.Equal(0, second.ExitCode);
        Assert.Equal(CliUnderTest.DisplayVersion + Environment.NewLine, second.Stdout);
    }

    [Fact(DisplayName = "update-check-background-notice: a background auto-reinstall is announced on the next command")]
    public async Task PendingNoticeIsPrintedOnceThenClearedAndSuppressedForChildProcesses()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        sandbox.SeedEmptyHome();
        sandbox.WriteHomeFile(
            ".effortless/update_check.json",
            """{"Preference":"always","LastCheckedDate":"2020-01-01","PendingNotice":"CLI auto-updated to the latest version."}""");

        var announced = await cli.Run(["-version"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, announced.ExitCode);
        Assert.Equal(
            "CLI auto-updated to the latest version." + Environment.NewLine +
            CliUnderTest.DisplayVersion + Environment.NewLine,
            announced.Stdout);

        var again = await cli.Run(["-version"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, again.ExitCode);
        Assert.Equal(CliUnderTest.DisplayVersion + Environment.NewLine, again.Stdout);

        sandbox.WriteHomeFile(
            ".effortless/update_check.json",
            """{"Preference":"always","LastCheckedDate":"2099-01-01","PendingNotice":"CLI auto-updated to the latest version."}""");

        var suppressed = await cli.Run(
            ["-version"],
            sandbox.ProjectPath,
            sandbox,
            environment: new Dictionary<string, string> { ["EFFORTLESS_CHILD_PROCESS"] = "1" });

        Assert.Equal(0, suppressed.ExitCode);
        Assert.Equal(CliUnderTest.DisplayVersion + Environment.NewLine, suppressed.Stdout);
    }
}
