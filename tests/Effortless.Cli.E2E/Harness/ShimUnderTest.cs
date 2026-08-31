using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Effortless.Cli.E2E.Harness;

internal sealed class ShimUnderTest
{
    private readonly CliUnderTest _cli;
    private readonly Sandbox _sandbox;

    private ShimUnderTest(string rootPath, CliUnderTest cli, Sandbox sandbox)
    {
        RootPath = rootPath;
        _cli = cli;
        _sandbox = sandbox;
    }

    public string RootPath { get; }

    public string ScriptPath => Path.Combine(RootPath, "cli.js");

    public string PackagePath => Path.Combine(RootPath, "package.json");

    public string CsprojPath =>
        IsRebuildFixture
            ? Path.Combine(
                RootPath,
                "src",
                "Effortless.Cli",
                "Effortless.Cli.csproj")
            : Path.Combine(
                RootPath,
                "Windows",
                "CLI",
                "SSoTme.OST.CLI.csproj");

    public string VersionConstantPath =>
        IsRebuildFixture
            ? Path.Combine(
                RootPath,
                "src",
                "Effortless.Cli.Core",
                "CliVersion.cs")
            : Path.Combine(
                RootPath,
                "Windows",
                "Lib",
                "CLIOptions",
                "SSoTmeCLIHandler.cs");

    private bool IsRebuildFixture =>
        File.Exists(
            Path.Combine(
                RootPath,
                "src",
                "Effortless.Cli",
                "Effortless.Cli.csproj"));

    public string AliasInstallPath =>
        Path.Combine(
            Path.GetDirectoryName(RootPath)
            ?? throw new InvalidOperationException("The shim root has no parent directory."),
            "npm-prefix");

    public static ShimUnderTest CreatePrebuilt(CliUnderTest cli, Sandbox sandbox)
    {
        var root = Path.Combine(sandbox.RootPath, "shim-prebuilt");
        Directory.CreateDirectory(root);
        CopyShim(root);
        CopyRootFile("package.json", root);
        string outputDestination;
        string expectedDll;
        if (Behavior.IsLegacy)
        {
            var legacyRoot = LegacyRepositoryRoot(cli);
            CopyRepositoryFileFrom(
                legacyRoot,
                Path.Combine(
                    "Windows",
                    "CLI",
                    "SSoTme.OST.CLI.csproj"),
                root);
            CopyRepositoryFileFrom(
                legacyRoot,
                Path.Combine(
                    "Windows",
                    "Lib",
                    "CLIOptions",
                    "SSoTmeCLIHandler.cs"),
                root);
            outputDestination = Path.Combine(
                root,
                "Windows",
                "CLI",
                "bin",
                "Release",
                "net8.0");
            expectedDll = Path.Combine(
                outputDestination,
                "SSoTme.OST.CLI.dll");
        }
        else
        {
            CopyDirectory(
                Path.Combine(CliUnderTest.Root, "src"),
                Path.Combine(root, "src"),
                excludeBuildArtifacts: true);
            outputDestination = Path.Combine(
                root,
                "src",
                "Effortless.Cli",
                "bin",
                "Release",
                "net8.0");
            expectedDll = Path.Combine(
                outputDestination,
                "Effortless.Cli.dll");
        }

        var outputSource = Path.GetDirectoryName(cli.DllPath)
            ?? throw new InvalidOperationException(
                "The CLI DLL has no containing directory.");
        CopyDirectory(outputSource, outputDestination);
        if (!File.Exists(expectedDll))
        {
            throw new FileNotFoundException(
                "The isolated shim fixture requires the actual CLI assembly for its source layout.",
                expectedDll);
        }

        AssertVersionSourcesAreSynchronized(root);
        return new ShimUnderTest(root, cli, sandbox);
    }

    public static ShimUnderTest CreateFullSource(CliUnderTest cli, Sandbox sandbox)
    {
        var root = Path.Combine(sandbox.RootPath, "shim-version-sync");
        Directory.CreateDirectory(root);
        CopyShim(root);
        CopyRootFile("package.json", root);
        if (Behavior.IsLegacy)
        {
            var legacyRoot = LegacyRepositoryRoot(cli);
            CopyRepositoryFileFrom(
                legacyRoot,
                "SSoTme-OST-CLI.sln",
                root);
            CopyDirectory(
                Path.Combine(legacyRoot, "Windows"),
                Path.Combine(root, "Windows"),
                excludeBuildArtifacts: true);
        }
        else
        {
            CopyRootFile("Effortless.Cli.sln", root);
            CopyDirectory(
                Path.Combine(CliUnderTest.Root, "src"),
                Path.Combine(root, "src"),
                excludeBuildArtifacts: true);
            CopyDirectory(
                Path.Combine(CliUnderTest.Root, "tests"),
                Path.Combine(root, "tests"),
                excludeBuildArtifacts: true);
        }
        return new ShimUnderTest(root, cli, sandbox);
    }

    public async Task<CliResult> RunNode(
        IReadOnlyList<string> args,
        string cwd,
        int timeoutMs = 120_000)
    {
        var nodeArgs = new[] { ScriptPath }.Concat(args).ToArray();
        return await RunProcessAsync("node", nodeArgs, cwd, timeoutMs);
    }

    public async Task InstallAliases()
    {
        Directory.CreateDirectory(AliasInstallPath);
        var npm = OperatingSystem.IsWindows() ? "npm.cmd" : "npm";
        var result = await RunProcessAsync(
            npm,
            [
                "install",
                "--global",
                RootPath,
                "--prefix",
                AliasInstallPath,
                "--ignore-scripts",
                "--no-audit",
                "--no-fund",
            ],
            RootPath,
            120_000);
        if (result.Failed)
        {
            throw new InvalidOperationException(
                $"Isolated npm alias installation failed.{Environment.NewLine}{result.Combined}");
        }

        foreach (var alias in PackageBinNames())
        {
            var path = AliasPath(alias);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"npm did not create the expected '{alias}' alias.",
                    path);
            }
        }
    }

    public async Task<CliResult> RunAlias(
        string alias,
        IReadOnlyList<string> args,
        string cwd,
        int timeoutMs = 120_000)
    {
        var aliasPath = AliasPath(alias);
        if (!File.Exists(aliasPath))
        {
            throw new FileNotFoundException(
                $"The isolated npm alias '{alias}' is not installed.",
                aliasPath);
        }

        if (!OperatingSystem.IsWindows())
        {
            return await RunProcessAsync(aliasPath, args, cwd, timeoutMs);
        }

        var command = string.Join(
            " ",
            new[] { QuoteForCommandPrompt(aliasPath) }
                .Concat(args.Select(QuoteForCommandPrompt)));
        return await RunProcessAsync(
            Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe",
            ["/d", "/s", "/c", command],
            cwd,
            timeoutMs);
    }

    public IReadOnlyList<string> PackageBinNames()
    {
        using var package = JsonDocument.Parse(File.ReadAllText(PackagePath));
        return package.RootElement.GetProperty("bin")
            .EnumerateObject()
            .Select(property =>
            {
                if (!string.Equals(property.Value.GetString(), "cli.js", StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Package alias '{property.Name}' does not point to cli.js.");
                }

                return property.Name;
            })
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    public void SetPackageVersion(string version)
    {
        var package = JsonNode.Parse(File.ReadAllText(PackagePath))?.AsObject()
            ?? throw new InvalidDataException("The isolated package.json is not an object.");
        package["version"] = version;
        File.WriteAllText(
            PackagePath,
            package.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
            + Environment.NewLine);
    }

    private string AliasPath(string alias)
    {
        var directory = OperatingSystem.IsWindows()
            ? AliasInstallPath
            : Path.Combine(AliasInstallPath, "bin");
        return Path.Combine(directory, alias + (OperatingSystem.IsWindows() ? ".cmd" : string.Empty));
    }

    private async Task<CliResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> args,
        string cwd,
        int timeoutMs)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        _cli.ApplySandboxEnvironment(startInfo, _sandbox);
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        startInfo.Environment["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "true";
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.Environment["NUGET_PACKAGES"] =
            Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget",
                "packages");
        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        process.Start();
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
            var capture = Task.WhenAll(stdoutTask, stderrTask);
            var captureCompleted = await Task.WhenAny(
                capture,
                Task.Delay(TimeSpan.FromSeconds(5)));
            var stdout = captureCompleted == capture && stdoutTask.IsCompletedSuccessfully
                ? stdoutTask.Result
                : "<stdout pipe remained open after process-tree termination>";
            var stderr = captureCompleted == capture && stderrTask.IsCompletedSuccessfully
                ? stderrTask.Result
                : "<stderr pipe remained open after process-tree termination>";
            throw new TimeoutException(
                $"Shim process exceeded {timeoutMs} ms.{Environment.NewLine}" +
                $"stdout:{Environment.NewLine}{stdout}{Environment.NewLine}" +
                $"stderr:{Environment.NewLine}{stderr}");
        }

        var completedStdout = await stdoutTask;
        var completedStderr = await stderrTask;
        stopwatch.Stop();
        return new CliResult(
            process.ExitCode,
            completedStdout,
            completedStderr,
            completedStdout + completedStderr,
            stopwatch.Elapsed);
    }

    private static void AssertVersionSourcesAreSynchronized(string root)
    {
        using var package = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, "package.json")));
        var packageVersion = package.RootElement.GetProperty("version").GetString()
            ?? throw new InvalidDataException("package.json has no version.");
        var match = Regex.Match(
            packageVersion,
            @"^(\d{4})-(\d{2})-(\d{2})\.(\d{1,2})\.(\d{1,2})$");
        var csprojVersion = match.Success
            ? $"{int.Parse(match.Groups[1].Value)}." +
              $"{int.Parse(match.Groups[2].Value)}." +
              $"{int.Parse(match.Groups[3].Value)}." +
              $"{int.Parse(match.Groups[4].Value)}" +
              $"{match.Groups[5].Value.PadLeft(2, '0')}"
            : packageVersion;

        var rebuildProject = Path.Combine(
            root,
            "src",
            "Effortless.Cli",
            "Effortless.Cli.csproj");
        var isRebuild = File.Exists(rebuildProject);
        var csproj = File.ReadAllText(
            isRebuild
                ? rebuildProject
                : Path.Combine(
                    root,
                    "Windows",
                    "CLI",
                    "SSoTme.OST.CLI.csproj"));
        var versionSource = File.ReadAllText(
            isRebuild
                ? Path.Combine(
                    root,
                    "src",
                    "Effortless.Cli.Core",
                    "CliVersion.cs")
                : Path.Combine(
                    root,
                    "Windows",
                    "Lib",
                    "CLIOptions",
                    "SSoTmeCLIHandler.cs"));
        var hasVersionConstant = isRebuild
            ? versionSource.Contains(
                $"public const string Value = \"{packageVersion}\";",
                StringComparison.Ordinal)
            : versionSource.Contains(
                $"public string CLI_VERSION = \"{packageVersion}\";",
                StringComparison.Ordinal);
        if (!csproj.Contains(
                $"<Version>{csprojVersion}</Version>",
                StringComparison.Ordinal)
            || !hasVersionConstant)
        {
            throw new InvalidOperationException(
                "The prebuilt shim fixture's version sources are not synchronized with package.json.");
        }
    }

    private static void CopyShim(string destinationRoot)
    {
        var source = Behavior.IsLegacy
            ? Path.Combine(
                CliUnderTest.Root,
                "tests",
                "fixtures",
                "shim",
                "legacy-cli.js")
            : Path.Combine(CliUnderTest.Root, "cli.js");
        var destination = Path.Combine(destinationRoot, "cli.js");
        File.Copy(
            source,
            destination);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                destination,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute);
        }
    }

    private static void CopyRootFile(string fileName, string destinationRoot) =>
        CopyRepositoryFile(fileName, destinationRoot);

    private static void CopyRepositoryFile(string relativePath, string destinationRoot)
    {
        CopyRepositoryFileFrom(
            CliUnderTest.Root,
            relativePath,
            destinationRoot);
    }

    private static void CopyRepositoryFileFrom(
        string repositoryRoot,
        string relativePath,
        string destinationRoot)
    {
        var source = Path.Combine(repositoryRoot, relativePath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException(
                $"Required shim fixture source '{relativePath}' does not exist.",
                source);
        }

        var destination = Path.Combine(destinationRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination);
    }

    private static string LegacyRepositoryRoot(
        CliUnderTest cli)
    {
        var directory = new DirectoryInfo(
            Path.GetDirectoryName(cli.DllPath)
            ?? throw new InvalidOperationException(
                "The legacy CLI DLL has no containing directory."));
        for (var index = 0; index < 5; index++)
        {
            directory = directory.Parent
                ?? throw new DirectoryNotFoundException(
                    "Could not locate the legacy worktree from the CLI DLL.");
        }

        return directory.FullName;
    }

    private static void CopyDirectory(
        string source,
        string destination,
        bool excludeBuildArtifacts = false)
    {
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException(
                $"Required shim fixture directory '{source}' does not exist.");
        }

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            var name = Path.GetFileName(directory);
            if (excludeBuildArtifacts
                && (name.Equals("bin", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            CopyDirectory(
                directory,
                Path.Combine(destination, name),
                excludeBuildArtifacts);
        }
    }

    private static string QuoteForCommandPrompt(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";

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
