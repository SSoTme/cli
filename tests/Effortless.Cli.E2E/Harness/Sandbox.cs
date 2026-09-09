using System.Text.Json;
using System.Text.Json.Nodes;

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
        return WriteUnder(ProjectPath, relativePath, contents);
    }

    public string WriteFile(string relativePath, byte[] contents)
    {
        var fullPath = ResolveUnder(ProjectPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, contents);
        return fullPath;
    }

    public string WriteHomeFile(string relativePath, string contents)
    {
        return WriteUnder(HomePath, relativePath, contents);
    }

    public string ReadFile(string relativePath)
    {
        return File.ReadAllText(ResolveUnder(ProjectPath, relativePath));
    }

    public JsonNode ReadJson(string relativePath)
    {
        var path = ResolveUnder(ProjectPath, relativePath);
        try
        {
            return JsonNode.Parse(File.ReadAllText(path))
                ?? throw new InvalidDataException($"JSON file '{relativePath}' contains null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"JSON file '{relativePath}' is malformed.", exception);
        }
    }

    public JsonNode ProjectFile => ReadJson("effortless.json");

    public void SeedEmptyHome()
    {
        var configPath = Path.Combine(HomePath, ".effortless");
        if (!Directory.Exists(configPath))
        {
            Directory.CreateDirectory(configPath);
        }
    }

    public void SeedHome(IndexFixture index, bool withToolUrls = true)
    {
        ArgumentNullException.ThrowIfNull(index);
        SeedEmptyHome();

        if (withToolUrls)
        {
            WriteHomeFile(
                ".effortless/tool_urls.json",
                JsonSerializer.Serialize(
                    index.ToolUrls,
                    new JsonSerializerOptions { WriteIndented = true }));
        }

        var catalog = JsonNode.Parse(index.Json)?.AsObject()
            ?? throw new InvalidDataException(
                "The index fixture is not a JSON object.");
        catalog["fetchedAt"] =
            CliUnderTest.TestUtcNow.ToString("O");
        WriteHomeFile(
            ".effortless/remote_tools/effortless-tools.json",
            catalog.ToJsonString(
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));
        WriteHomeFile(
            ".effortless/remote_tools/cli_version",
            CliUnderTest.PackageVersion);
        WriteHomeFile(
            ".effortless/remote_tools/effortless.json",
            """
            {
              "Name": "remote_tools",
              "ProjectSettings": [
                {
                  "Name": "project-name",
                  "Value": "remote_tools"
                }
              ],
              "ProjectTranspilers": []
            }
            """);
    }

    public void SeedEmptyIndex(IndexFixture index)
    {
        ArgumentNullException.ThrowIfNull(index);
        SeedHome(index);
        WriteHomeFile(
            ".effortless/remote_tools/effortless-tools.json",
            """{"transpilerVersions":{}}""");
    }

    /// <summary>
    /// Puts a fake `npm` on the sandboxed PATH (ahead of any real npm) that
    /// appends its arguments to <paramref name="logRelativePath"/> under the
    /// project directory instead of installing anything, for tests that
    /// assert whether UpdateChecker invoked "npm install -g ...@latest".
    /// </summary>
    public void WriteFakeNpm(string logRelativePath)
    {
        var logPath = ResolveUnder(ProjectPath, logRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(
                Path.Combine(BinPath, "npm.cmd"),
                $"@echo %* >> \"{logPath}\"{Environment.NewLine}");
            return;
        }

        var path = Path.Combine(BinPath, "npm");
        File.WriteAllText(path, $"#!/bin/sh\necho \"$@\" >> \"{logPath}\"\n");
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

    public void SeedProject(string fixtureName)
    {
        if (string.IsNullOrWhiteSpace(fixtureName)
            || fixtureName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The project fixture name is invalid.", nameof(fixtureName));
        }

        var source = Path.Combine(
            CliUnderTest.Root,
            "tests",
            "fixtures",
            "projects",
            fixtureName);
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException(
                $"Project fixture '{fixtureName}' does not exist at '{source}'.");
        }

        CopyDirectory(source, ProjectPath);
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

    private static string WriteUnder(string root, string relativePath, string contents)
    {
        var fullPath = ResolveUnder(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents);
        return fullPath;
    }

    private static string ResolveUnder(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Sandbox paths must be relative.", nameof(relativePath));
        }

        var normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Sandbox path '{relativePath}' escapes '{root}'.");
        }

        return fullPath;
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(
            source,
            "*",
            SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(
                Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
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
