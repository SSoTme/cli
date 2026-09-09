using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Effortless.Cli.Config;
using Effortless.Cli.Updates;

namespace Effortless.Cli.Tests;

/// <summary>
/// -checkVersion / UpdateChecker. Persistence goes through the real
/// UserConfigDir.EffortlessDir ($HOME/.effortless), same as ToolUrls/JwtStore,
/// so each test redirects HOME to an isolated TestDirectory. Safe because
/// this assembly disables parallelization (see AssemblyInfo.cs).
/// </summary>
public sealed class UpdateCheckerTests
{
    private const string NewSha = "2222222222222222222222222222222222222222";

    [Theory(DisplayName = "unit-check-version-prompt-flow: checkVersion prompts Yes/No/Always/Never on a changed commit")]
    [InlineData("y", true, null)]
    [InlineData("n", false, null)]
    [InlineData("a", true, "always")]
    [InlineData("e", false, "never")]
    public void PromptResponsesInvokeNpmAndPersistCorrectly(
        string response,
        bool expectNpmInstall,
        string expectedPreference)
    {
        using var home = new TestDirectory();
        using var homeOverride = new HomeOverride(home.Path);

        var invoked = new List<ProcessStartInfo>();
        var checker = new UpdateChecker(
            httpClient: new HttpClient(new ShaHandler(NewSha)),
            runProcess: invoked.Add);

        var (output, exitCode) = RunWithStdin(checker, response);

        Assert.Equal(0, exitCode);
        Assert.Equal(expectNpmInstall, invoked.Count == 1);
        if (expectNpmInstall)
        {
            var call = Assert.Single(invoked);
            Assert.Equal("npm", call.FileName);
            Assert.Equal(["install", "-g", "@effortlessapi/cli@latest"], call.ArgumentList);
            Assert.Contains("Reinstalled.", output, StringComparison.Ordinal);
            if (expectedPreference == "always")
            {
                Assert.Contains(
                    "Always: future background checks will auto-reinstall silently.",
                    output,
                    StringComparison.Ordinal);
            }
        }
        else if (expectedPreference == "never")
        {
            Assert.Contains(
                "Never: future background checks will not act until you run -checkVersion again.",
                output,
                StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("Skipped. Run -checkVersion again any time.", output, StringComparison.Ordinal);
        }

        Assert.Equal(expectedPreference, UpdateCheckState.GetPreference());
    }

    [Fact(DisplayName = "unit-check-version-unchanged: checkVersion reports up to date without prompting")]
    public void MatchingShaReportsUpToDateWithoutPrompting()
    {
        using var home = new TestDirectory();
        using var homeOverride = new HomeOverride(home.Path);

        var invoked = new List<ProcessStartInfo>();
        var checker = new UpdateChecker(
            httpClient: new HttpClient(new ShaHandler(CliVersion.CommitSha)),
            runProcess: invoked.Add);

        // No stdin needed: an unchanged sha must never read from Console.In.
        var output = RunCapturingOutput(() => checker.RunExplicit());

        Assert.Contains("Already on the latest version", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Reinstall now?", output, StringComparison.Ordinal);
        Assert.Empty(invoked);
        Assert.Null(UpdateCheckState.GetPreference());
    }

    [Theory(DisplayName = "unit-check-version-always-reprompts: explicit checkVersion always asks even when Always/Never is stored")]
    [InlineData("always")]
    [InlineData("never")]
    public void ExplicitRunAlwaysPromptsEvenWithStoredPreference(string storedPreference)
    {
        using var home = new TestDirectory();
        using var homeOverride = new HomeOverride(home.Path);
        UpdateCheckState.SetPreference(storedPreference);

        var invoked = new List<ProcessStartInfo>();
        var checker = new UpdateChecker(
            httpClient: new HttpClient(new ShaHandler(NewSha)),
            runProcess: invoked.Add);

        var (output, _) = RunWithStdin(checker, "n");

        Assert.Contains("Reinstall now? (Y)es / (N)o / (A)lways / n(E)ver:", output, StringComparison.Ordinal);
        Assert.Empty(invoked);
        // A "no" response never overwrites a previously stored preference.
        Assert.Equal(storedPreference, UpdateCheckState.GetPreference());
    }

    [Fact(DisplayName = "unit-update-check-background-preference-gate: background check only acts on a stored Always preference")]
    public async Task BackgroundCheckHonorsStoredPreferenceSilently()
    {
        using var home = new TestDirectory();
        using var homeOverride = new HomeOverride(home.Path);

        // No preference set: no-op, no npm call, no console output.
        var neverInvoked = new List<ProcessStartInfo>();
        var noPreferenceChecker = new UpdateChecker(
            httpClient: new HttpClient(new ShaHandler(NewSha)),
            runProcess: neverInvoked.Add);
        await noPreferenceChecker.RunBackgroundAsync();
        Assert.Empty(neverInvoked);
        Assert.Null(UpdateCheckState.TakePendingNotice());

        // Preference=never: gated the same way, no npm call ever.
        UpdateCheckState.SetPreference("never");
        var neverPreferenceInvoked = new List<ProcessStartInfo>();
        var neverChecker = new UpdateChecker(
            httpClient: new HttpClient(new ShaHandler(NewSha)),
            runProcess: neverPreferenceInvoked.Add);
        await neverChecker.RunBackgroundAsync();
        Assert.Empty(neverPreferenceInvoked);
        Assert.Null(UpdateCheckState.TakePendingNotice());

        // Preference=always + changed sha: silent reinstall, pending notice set.
        UpdateCheckState.SetPreference("always");
        var alwaysInvoked = new List<ProcessStartInfo>();
        var alwaysChecker = new UpdateChecker(
            httpClient: new HttpClient(new ShaHandler(NewSha)),
            runProcess: alwaysInvoked.Add);
        var output = await RunCapturingOutputAsync(() => alwaysChecker.RunBackgroundAsync());
        Assert.Single(alwaysInvoked);
        Assert.Empty(output);
        Assert.Equal(
            "CLI auto-updated to the latest version.",
            UpdateCheckState.TakePendingNotice());
        // TakePendingNotice clears it: a second read returns nothing.
        Assert.Null(UpdateCheckState.TakePendingNotice());
    }

    [Fact(DisplayName = "unit-update-check-background-exceptions-swallowed: background check never throws")]
    public async Task BackgroundCheckSwallowsExceptions()
    {
        using var home = new TestDirectory();
        using var homeOverride = new HomeOverride(home.Path);
        UpdateCheckState.SetPreference("always");

        var checker = new UpdateChecker(
            httpClient: new HttpClient(new ThrowingHandler()),
            runProcess: _ => throw new InvalidOperationException("should not be reached"));

        await checker.RunBackgroundAsync();
    }

    private static (string Output, int ExitCode) RunWithStdin(UpdateChecker checker, string stdin)
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;
        try
        {
            using var reader = new StringReader(stdin + Environment.NewLine);
            using var writer = new StringWriter();
            Console.SetIn(reader);
            Console.SetOut(writer);
            var exitCode = checker.RunExplicit();
            return (writer.ToString(), exitCode);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    private static string RunCapturingOutput(Func<int> action)
    {
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static async Task<string> RunCapturingOutputAsync(Func<Task> action)
    {
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            await action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    /// Redirects UserConfigDir.EffortlessDir (HOME-based) to an isolated
    /// directory for the lifetime of one test.
    /// </summary>
    private sealed class HomeOverride : IDisposable
    {
        private readonly string? _originalHome;
        private readonly string? _originalUserProfile;

        public HomeOverride(string path)
        {
            _originalHome = Environment.GetEnvironmentVariable("HOME");
            _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            Environment.SetEnvironmentVariable("HOME", path);
            Environment.SetEnvironmentVariable("USERPROFILE", path);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("HOME", _originalHome);
            Environment.SetEnvironmentVariable("USERPROFILE", _originalUserProfile);
        }
    }

    private sealed class ShaHandler : HttpMessageHandler
    {
        private readonly string _sha;

        public ShaHandler(string sha)
        {
            _sha = sha;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Contains(
                "/repos/EffortlessAPI/cli/commits/main",
                request.RequestUri!.AbsolutePath,
                StringComparison.Ordinal);
            var body = JsonSerializer.Serialize(new { sha = _sha });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body),
            });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("simulated network failure");
    }
}
