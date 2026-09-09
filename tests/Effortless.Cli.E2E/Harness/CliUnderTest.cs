using System.Diagnostics;
using System.Text.Json;

namespace Effortless.Cli.E2E.Harness;

internal sealed record CliResult(
    int ExitCode,
    string Stdout,
    string Stderr,
    string Combined,
    TimeSpan Duration)
{
    public bool Failed => ExitCode != 0;
}

internal sealed class CliUnderTest
{
    private static readonly Lazy<string> RepositoryRoot = new(FindRepositoryRoot);
    public static readonly DateTimeOffset TestUtcNow =
        new(2026, 8, 31, 6, 0, 0, TimeSpan.Zero);

    public CliUnderTest(string? dllPath = null)
    {
        DllPath = Path.GetFullPath(
            dllPath
            ?? Environment.GetEnvironmentVariable("EFFORTLESS_CLI_UNDER_TEST")
            ?? GetDefaultDllPath());

        if (!File.Exists(DllPath))
        {
            throw new FileNotFoundException(
                "The CLI under test does not exist. Build the solution or set EFFORTLESS_CLI_UNDER_TEST.",
                DllPath);
        }
    }

    public string DllPath { get; }

    public static string Root => RepositoryRoot.Value;


    public static string PackageVersion
    {
        get
        {
            using var package = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "package.json")));
            return package.RootElement.GetProperty("version").GetString()
                ?? throw new InvalidDataException("package.json has no version.");
        }
    }

    /// <summary>
    /// The human-unambiguous form of <see cref="PackageVersion"/>, mirroring
    /// cli.js's syncVersionFromPackageJson: "v{yyyy}-{MM}-{dd}-{HHmm}" (24h UTC).
    /// </summary>
    public static string DisplayVersion
    {
        get
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                PackageVersion,
                @"^(\d{4})\.(\d{3,4})\.(\d{1,4})$");
            if (!match.Success)
            {
                throw new InvalidDataException($"package.json version '{PackageVersion}' is not npm-safe YYYY.MDD.HHMM.");
            }

            var year = match.Groups[1].Value;
            var monthDay = int.Parse(match.Groups[2].Value);
            var hourMinute = int.Parse(match.Groups[3].Value);
            var month = monthDay / 100;
            var day = monthDay % 100;
            return $"v{year}-{month:D2}-{day:D2}-{hourMinute:D4}";
        }
    }

    public async Task<CliResult> Run(
        IReadOnlyList<string> args,
        string cwd,
        Sandbox sandbox,
        string? stdin = null,
        int timeoutMs = 120_000,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(DllPath);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }
        ApplySandboxEnvironment(startInfo, sandbox);
        foreach (var (key, value) in environment ?? new Dictionary<string, string>())
        {
            startInfo.Environment[key] = value;
        }

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        process.Start();

        if (stdin is not null)
        {
            await process.StandardInput.WriteAsync(stdin);
            process.StandardInput.Close();
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(timeoutMs);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            TryKillTree(process);
            var timedOutStdout = await stdoutTask;
            var timedOutStderr = await stderrTask;
            throw new TimeoutException(
                $"CLI exceeded {timeoutMs} ms.{Environment.NewLine}" +
                $"stdout:{Environment.NewLine}{timedOutStdout}{Environment.NewLine}" +
                $"stderr:{Environment.NewLine}{timedOutStderr}");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        stopwatch.Stop();
        return new CliResult(
            process.ExitCode,
            stdout,
            stderr,
            stdout + stderr,
            stopwatch.Elapsed);
    }

    public void ApplySandboxEnvironment(ProcessStartInfo startInfo, Sandbox sandbox)
    {
        startInfo.Environment["HOME"] = sandbox.HomePath;
        startInfo.Environment["USERPROFILE"] = sandbox.HomePath;
        startInfo.Environment["PATH"] =
            sandbox.BinPath + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? "");
        startInfo.Environment.Remove("EFFORTLESS_CHILD_PROCESS");
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["TERM"] = "dumb";
        startInfo.Environment["EFFORTLESS_CLI_TEST_UTC_NOW"] =
            TestUtcNow.ToString("O");
    }

    private static string GetDefaultDllPath()
    {
        return Path.Combine(
            Root,
            "src",
            "Effortless.Cli",
            "bin",
            "Release",
            "net8.0",
            "Effortless.Cli.dll");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "package.json"))
                && Directory.Exists(Path.Combine(directory.FullName, "effortless-rulebook")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the effortless-cli repository root.");
    }

    private static void TryKillTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the checks.
        }
    }
}
