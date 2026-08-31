using System.Diagnostics;

namespace Effortless.Cli.Seeds;

public sealed class SeedRepositoryManager
{
    private readonly Func<ProcessStartInfo, int> _runProcess;

    public SeedRepositoryManager(
        Func<ProcessStartInfo, int> runProcess = null)
    {
        _runProcess = runProcess ?? RunProcess;
    }

    public string Clone(
        string cloneUrl,
        string directoryName)
    {
        if (!Uri.TryCreate(
                cloneUrl,
                UriKind.Absolute,
                out var uri)
            || uri.Scheme is not ("https" or "http"))
        {
            throw new ArgumentException(
                "Seed clone URL must be an absolute HTTP(S) URL.",
                nameof(cloneUrl));
        }

        if (string.IsNullOrWhiteSpace(directoryName))
        {
            throw new ArgumentException(
                "Seed clone directory is required.",
                nameof(directoryName));
        }

        var destination = Path.GetFullPath(directoryName);
        if (Directory.Exists(destination)
            || File.Exists(destination))
        {
            throw new IOException(
                $"Seed destination already exists: {destination}");
        }

        var startInfo = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("clone");
        startInfo.ArgumentList.Add(uri.ToString());
        startInfo.ArgumentList.Add(destination);
        var exitCode = _runProcess(startInfo);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"git clone failed with exit code {exitCode}.");
        }

        if (!File.Exists(
                Path.Combine(destination, "effortless.json")))
        {
            throw new InvalidDataException(
                $"Cloned repository does not contain effortless.json at its root: {destination}");
        }

        return destination;
    }

    private static int RunProcess(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Unable to start git.");
        process.WaitForExit();
        return process.ExitCode;
    }
}
