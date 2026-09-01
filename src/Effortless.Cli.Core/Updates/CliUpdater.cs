using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli.Updates;

public sealed class CliUpdater
{
    private readonly HttpClient _httpClient;
    private readonly Action<string> _launchInstaller;

    public CliUpdater(
        HttpClient httpClient = null,
        Action<string> launchInstaller = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "effortless-cli");
        _launchInstaller = launchInstaller ?? LaunchInstaller;
    }

    public int Run()
    {
        Console.WriteLine(
            "Checking GitHub for the latest CLI version...");
        try
        {
            var release = JObject.Parse(
                _httpClient.GetStringAsync(
                        "https://api.github.com/repos/EffortlessAPI/cli/releases/latest")
                    .GetAwaiter()
                    .GetResult());
            var tag = release["tag_name"]?.Value<string>();
            if (string.IsNullOrEmpty(tag))
            {
                Console.WriteLine(
                    "Could not determine the latest version from GitHub.");
                return 0;
            }

            if (Parse(tag).CompareTo(Parse(CliVersion.Value)) <= 0)
            {
                Console.WriteLine(
                    $"Already on the latest version ({CliVersion.Value}).");
                return 0;
            }

            Console.WriteLine(
                $"New version available: {tag}  (you have {CliVersion.Value})");
            Console.WriteLine(
                "To upgrade: npm install -g @effortlessapi/cli@latest");
            Console.WriteLine(
                "Download from: https://github.com/EffortlessAPI/cli/releases/latest");
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"Could not reach GitHub: {exception.Message}");
            Console.WriteLine(
                "To upgrade from npm: npm install -g @effortlessapi/cli@latest");
            Console.WriteLine(
                "Download manually: https://github.com/EffortlessAPI/cli/releases/latest");
        }

        return 0;
    }

    private static Version Parse(string value)
    {
        var digits = new string(
            (value ?? string.Empty)
            .Select(character => char.IsDigit(character)
                ? character
                : '.')
            .ToArray());
        var parts = digits.Split(
                '.',
                StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var number)
                ? number
                : 0)
            .Concat(Enumerable.Repeat(0, 5))
            .Take(4)
            .ToArray();
        return new Version(parts[0], parts[1], parts[2], parts[3]);
    }

    private static void LaunchInstaller(string path)
    {
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
    }
}
