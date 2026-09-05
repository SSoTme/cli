#nullable enable
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;

namespace Effortless.Cli.LocalTools;

/// <summary>
/// Runs a <see cref="LocalToolRuntime.Script"/> tool once: unpacks the input
/// fileset, invokes the entry with the EFFORTLESS_* directory contract, and
/// packs whatever it wrote into the output fileset.
/// </summary>
internal static class ScriptToolRunner
{
    public static async Task<LocalToolRunResult> RunAsync(
        LocalTool tool,
        TranspilePayload request,
        string workRoot,
        CancellationToken cancellationToken)
    {
        var runDirectory = Path.Combine(workRoot, $"run-{Guid.NewGuid():N}");
        var inputDirectory = Path.Combine(runDirectory, "input");
        var outputDirectory = Path.Combine(runDirectory, "output");
        var result = new LocalToolRunResult();
        try
        {
            LocalFileSets.WriteInputDirectory(request.CLIInputFileSetXml, inputDirectory);
            Directory.CreateDirectory(outputDirectory);

            var startInfo = CreateStartInfo(tool, out var executable);
            startInfo.Environment["EFFORTLESS_INPUT_DIR"] = inputDirectory;
            startInfo.Environment["EFFORTLESS_OUTPUT_DIR"] = outputDirectory;
            startInfo.Environment["EFFORTLESS_OUTPUT_NAME"] = request.CLIOutput ?? string.Empty;
            startInfo.Environment["EFFORTLESS_PARAMS"] =
                JsonConvert.SerializeObject(request.CLIParams ?? Array.Empty<string>());
            startInfo.Environment["EFFORTLESS_TOOL_NAME"] = tool.Name;

            using var process = new Process { StartInfo = startInfo };
            var stdout = new List<string>();
            var stderr = new List<string>();
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) { lock (stdout) stdout.Add(e.Data); } };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { lock (stderr) stderr.Add(e.Data); } };

            try
            {
                process.Start();
            }
            catch (Win32Exception)
            {
                return LocalToolRunResult.Error(
                    $"ERROR: Local tool '{tool.Name}' needs the '{tool.RuntimeName}' runtime ('{executable}') which was not found on PATH.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var budget = request.CLIWaitTimeout > 0
                ? TimeSpan.FromMilliseconds(request.CLIWaitTimeout)
                : TimeSpan.FromMinutes(3);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(budget);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                AppendLogs(result, stdout, stderr, failed: true);
                result.ErrorMessage =
                    $"Local tool '{tool.Name}' did not finish within {budget.TotalMilliseconds:0} ms.";
                result.Logs.Add(new LogEntry("error", result.ErrorMessage));
                return result;
            }

            // Flush the async readers.
            process.WaitForExit();
            var failed = process.ExitCode != 0;
            AppendLogs(result, stdout, stderr, failed);
            if (failed)
            {
                result.ErrorMessage = $"Local tool '{tool.Name}' exited with code {process.ExitCode}.";
                result.Logs.Add(new LogEntry("error", result.ErrorMessage));
                return result;
            }

            result.OutputFileSetXml = LocalFileSets.ReadOutputDirectory(outputDirectory);
            return result;
        }
        finally
        {
            TryDelete(runDirectory);
        }
    }

    internal static ProcessStartInfo CreateStartInfo(LocalTool tool, out string executable)
    {
        var extension = Path.GetExtension(tool.Entry).ToLowerInvariant();
        var isWindows = OperatingSystem.IsWindows();
        string? interpreter = extension switch
        {
            ".sh" => "sh",
            ".bash" => "bash",
            ".py" => isWindows ? "python" : "python3",
            ".js" or ".mjs" or ".cjs" => "node",
            ".ps1" => "pwsh",
            ".rb" => "ruby",
            _ => null,
        };

        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = tool.Directory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        if (interpreter is null)
        {
            executable = tool.Entry;
            startInfo.FileName = tool.Entry;
        }
        else
        {
            executable = interpreter;
            startInfo.FileName = interpreter;
            startInfo.ArgumentList.Add(tool.Entry);
        }

        return startInfo;
    }

    private static void AppendLogs(
        LocalToolRunResult result,
        List<string> stdout,
        List<string> stderr,
        bool failed)
    {
        lock (stdout)
        {
            foreach (var line in stdout)
            {
                result.Logs.Add(new LogEntry("message", line));
            }
        }

        lock (stderr)
        {
            foreach (var line in stderr)
            {
                result.Logs.Add(new LogEntry(failed ? "error" : "warning", line));
            }
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort.
        }
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best effort; the run directory lives under the gitignored ledger dir.
        }
    }
}
