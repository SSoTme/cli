using System.Diagnostics;
using System.Text;

namespace Effortless.Cli.E2E.Harness;

/// <summary>
/// A CLI process left running (e.g. <c>effortless serve</c>) while the test
/// runs other commands against it.
/// </summary>
internal sealed class BackgroundCli : IDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();

    private BackgroundCli(Process process)
    {
        _process = process;
        process.OutputDataReceived += (_, e) => Append(_stdout, e.Data);
        process.ErrorDataReceived += (_, e) => Append(_stderr, e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    public int Pid => _process.Id;

    public bool HasExited => _process.HasExited;

    public string Stdout
    {
        get
        {
            lock (_stdout)
            {
                return _stdout.ToString();
            }
        }
    }

    public string Stderr
    {
        get
        {
            lock (_stderr)
            {
                return _stderr.ToString();
            }
        }
    }

    public static BackgroundCli Start(
        CliUnderTest cli,
        IReadOnlyList<string> args,
        string cwd,
        Sandbox sandbox,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(cli.DllPath);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        cli.ApplySandboxEnvironment(startInfo, sandbox);
        foreach (var (key, value) in environment ?? new Dictionary<string, string>())
        {
            startInfo.Environment[key] = value;
        }

        var process = new Process { StartInfo = startInfo };
        process.Start();
        return new BackgroundCli(process);
    }

    public async Task WaitForExitAsync(int timeoutMs = 60_000)
    {
        using var timeout = new CancellationTokenSource(timeoutMs);
        await _process.WaitForExitAsync(timeout.Token);
        _process.WaitForExit();
    }

    /// <summary>Polls until <paramref name="condition"/> holds or the timeout passes.</summary>
    public async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 30_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    $"The background CLI exited early with code {_process.ExitCode}.{Environment.NewLine}{Stdout}{Stderr}");
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException(
                    $"Condition not met within {timeoutMs} ms.{Environment.NewLine}{Stdout}{Stderr}");
            }

            await Task.Delay(100);
        }
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }

        _process.Dispose();
    }

    private static void Append(StringBuilder buffer, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (buffer)
        {
            buffer.AppendLine(line);
        }
    }
}
