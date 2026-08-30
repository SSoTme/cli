namespace Effortless.Cli.E2E.Harness;

internal sealed class Sandbox : IDisposable
{
    private bool _disposed;

    private Sandbox(string rootPath)
    {
        RootPath = rootPath;
        HomePath = Path.Combine(rootPath, "home");
        ProjectPath = Path.Combine(rootPath, "proj");
        BinPath = Path.Combine(rootPath, "bin");

        Directory.CreateDirectory(HomePath);
        Directory.CreateDirectory(ProjectPath);
        Directory.CreateDirectory(BinPath);
    }

    public string RootPath { get; }

    public string HomePath { get; }

    public string ProjectPath { get; }

    public string BinPath { get; }

    public static Sandbox Create(CliUnderTest cli)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "effortless-cli-e2e",
            $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}");
        var sandbox = new Sandbox(root);
        sandbox.WritePathShims(cli);
        return sandbox;
    }

    public string WriteFile(string relativePath, string contents)
    {
        var fullPath = Path.Combine(ProjectPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents);
        return fullPath;
    }

    public void SeedEmptyHome()
    {
        var configPath = Path.Combine(HomePath, ".ssotme");
        if (!Directory.Exists(configPath))
        {
            Directory.CreateDirectory(configPath);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DeleteWithRetry(RootPath);
    }

    private void WritePathShims(CliUnderTest cli)
    {
        foreach (var alias in new[] { "effortless", "ssotme", "aicapture", "aic" })
        {
            if (OperatingSystem.IsWindows())
            {
                File.WriteAllText(
                    Path.Combine(BinPath, alias + ".cmd"),
                    $"@dotnet \"{cli.DllPath}\" %*{Environment.NewLine}");
                continue;
            }

            var path = Path.Combine(BinPath, alias);
            File.WriteAllText(path, $"#!/bin/sh\nexec dotnet \"{cli.DllPath}\" \"$@\"\n");
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute);
        }
    }

    private static void DeleteWithRetry(string path)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(attempt * 50);
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                Thread.Sleep(attempt * 50);
            }
        }
    }
}
