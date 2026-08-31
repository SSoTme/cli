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
                "The CLI under test does not exist. Build the legacy solution or set EFFORTLESS_CLI_UNDER_TEST.",
                DllPath);
        }
    }

    public string DllPath { get; }

    public static string Root => RepositoryRoot.Value;

    public static bool IsLegacy =>
        string.Equals(
            Environment.GetEnvironmentVariable("EFFORTLESS_CLI_MODE"),
            "legacy",
            StringComparison.OrdinalIgnoreCase);

    public static string PackageVersion
    {
        get
        {
            using var package = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "package.json")));
            return package.RootElement.GetProperty("version").GetString()
                ?? throw new InvalidDataException("package.json has no version.");
        }
    }

    public async Task<CliResult> Run(
        IReadOnlyList<string> args,
        string cwd,
        Sandbox sandbox,
        string? stdin = null,
        int timeoutMs = 120_000)
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
        startInfo.Environment.Remove("SSOTME_CHILD_PROCESS");
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["TERM"] = "dumb";
        startInfo.Environment["EFFORTLESS_CLI_TEST_UTC_NOW"] =
            TestUtcNow.ToString("O");
    }

    private static string GetDefaultDllPath()
    {
        var legacy = Path.Combine(
            Root,
            "Windows",
            "CLI",
            "bin",
            "Release",
            "net8.0",
            "SSoTme.OST.CLI.dll");
        if (File.Exists(legacy))
        {
            return legacy;
        }

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
