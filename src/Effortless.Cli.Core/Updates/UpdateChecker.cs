using System.Diagnostics;
using System.Net.Http.Headers;
using Effortless.Cli.Config;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli.Updates;

/// <summary>
/// Retires CliUpdater/UpgradeCliCommand. Backs -checkVersion (see
/// <see cref="RunExplicit"/>, always prompts) and the once-daily background
/// check wired from Program.Main (see <see cref="RunBackgroundAsync"/>, never
/// prompts, honors the stored preference, and must never throw or write to
/// the console since it runs concurrently with the foreground command).
///
/// Compares the commit this CLI was built from (<see cref="CliVersion.CommitSha"/>)
/// against the latest commit on GitHub's main branch, rather than comparing
/// release tags: main advances between releases and a "latest release" tag
/// comparison would miss those commits.
/// </summary>
public sealed class UpdateChecker
{
    public const string ApiBaseEnvironmentVariable = "EFFORTLESS_UPDATE_GITHUB_API";

    private readonly HttpClient _httpClient;
    private readonly Action<ProcessStartInfo> _runProcess;
    private readonly string _apiBase;

    public UpdateChecker(
        HttpClient httpClient = null,
        Action<ProcessStartInfo> runProcess = null)
    {
        _apiBase = (Environment.GetEnvironmentVariable(ApiBaseEnvironmentVariable)
                    ?? "https://api.github.com").TrimEnd('/');
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("effortless-cli", CliVersion.Value));
        }

        _runProcess = runProcess ?? (startInfo =>
        {
            using var process = Process.Start(startInfo);
            process?.WaitForExit();
        });
    }

    /// <summary>
    /// -checkVersion. Always prompts on a changed commit, regardless of any
    /// stored preference — only the background check honors the stored
    /// preference silently.
    /// </summary>
    public int RunExplicit()
    {
        Console.WriteLine("Checking GitHub for the latest commit on main...");
        try
        {
            var sha = FetchLatestMainSha();
            if (string.Equals(sha, CliVersion.CommitSha, StringComparison.Ordinal))
            {
                Console.WriteLine(
                    $"Already on the latest version ({CliVersion.DisplayVersion}).");
                return 0;
            }

            Console.WriteLine(
                $"New commit available on main (you have {CliVersion.DisplayVersion}).");
            Console.Write("Reinstall now? (Y)es / (N)o / (A)lways / n(E)ver: ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();
            switch (response)
            {
                case "y" or "yes":
                    ReinstallNow();
                    break;
                case "a" or "always":
                    UpdateCheckState.SetPreference("always");
                    ReinstallNow();
                    Console.WriteLine(
                        "Always: future background checks will auto-reinstall silently.");
                    break;
                case "e" or "never":
                    UpdateCheckState.SetPreference("never");
                    Console.WriteLine(
                        "Never: future background checks will not act until you run -checkVersion again.");
                    break;
                default:
                    Console.WriteLine("Skipped. Run -checkVersion again any time.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not reach GitHub: {ex.Message}");
        }

        return 0;
    }

    /// <summary>
    /// Called once per process at startup when today's date has not yet been
    /// checked. Never prompts, never writes to the console (it can run
    /// concurrently with the foreground command's own output), and never
    /// throws — every failure is swallowed.
    /// </summary>
    public async Task RunBackgroundAsync()
    {
        try
        {
            var preference = UpdateCheckState.GetPreference();
            if (string.IsNullOrEmpty(preference))
            {
                return;
            }

            if (string.Equals(preference, "never", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!string.Equals(preference, "always", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var sha = await FetchLatestMainShaAsync().ConfigureAwait(false);
            if (string.Equals(sha, CliVersion.CommitSha, StringComparison.Ordinal))
            {
                return;
            }

            ReinstallSilently();
            UpdateCheckState.SetPendingNotice("CLI auto-updated to the latest version.");
        }
        catch
        {
            // Background: swallow everything, never interleave with the
            // foreground command's own output.
        }
    }

    private void ReinstallNow()
    {
        Console.WriteLine("Reinstalling via npm install -g @effortlessapi/cli@latest...");
        ReinstallSilently();
        Console.WriteLine(
            "Reinstalled. Restart your shell or open a new terminal to use the new version.");
    }

    private void ReinstallSilently() =>
        _runProcess(new ProcessStartInfo("npm")
        {
            ArgumentList = { "install", "-g", "@effortlessapi/cli@latest" },
            UseShellExecute = false,
        });

    private string FetchLatestMainSha() =>
        FetchLatestMainShaAsync().GetAwaiter().GetResult();

    private async Task<string> FetchLatestMainShaAsync()
    {
        var json = await _httpClient
            .GetStringAsync($"{_apiBase}/repos/EffortlessAPI/cli/commits/main")
            .ConfigureAwait(false);
        return JObject.Parse(json)["sha"]?.Value<string>();
    }
}
