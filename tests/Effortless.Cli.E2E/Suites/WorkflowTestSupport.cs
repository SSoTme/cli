using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

internal sealed record WorkflowStep(
    string Name,
    string RelativePath,
    string CommandLine,
    bool IsDisabled = false,
    string? TranspilerGroup = null,
    string? PinnedVersion = null,
    string? LastVersionUsed = null,
    string? LastUrl = null);

internal static class WorkflowTestSupport
{
    public const string HeadVersion = "v2026.01.01.0001";
    public const string OldVersion = "v2025.12.31.2359";

    private static readonly JsonSerializerOptions IndentedJson = new()
    {
        WriteIndented = true,
    };

    public static Sandbox CreateProjectSandbox(
        CliUnderTest cli,
        MockToolServer server,
        params WorkflowStep[] steps)
    {
        var index = IndexFixture.Load(server);
        server.IndexJson = index.Json;
        var sandbox = Sandbox.Create(cli);
        sandbox.SeedHome(index, withToolUrls: false);
        WriteProject(sandbox, steps);
        return sandbox;
    }

    public static void WriteProject(Sandbox sandbox, params WorkflowStep[] steps) =>
        WriteProjectAt(sandbox.ProjectPath, steps);

    public static void WriteProjectAt(string directory, params WorkflowStep[] steps)
    {
        Directory.CreateDirectory(directory);
        var root = new JsonObject
        {
            ["Name"] = "workflow-project",
            ["SSoTmeProjectId"] = "5d75f38e-b64e-48fe-bf20-53adfa346c8d",
            ["ProjectSettings"] = new JsonArray
            {
                new JsonObject
                {
                    ["Name"] = "project-name",
                    ["Value"] = "workflow-project",
                },
            },
            ["ProjectTranspilers"] = new JsonArray(
                steps.Select(ToJson).ToArray()),
        };
        File.WriteAllText(
            Path.Combine(directory, "effortless.json"),
            root.ToJsonString(IndentedJson) + Environment.NewLine);
    }

    public static JsonArray Steps(Sandbox sandbox) =>
        sandbox.ProjectFile["ProjectTranspilers"]?.AsArray()
        ?? throw new InvalidDataException("effortless.json has no ProjectTranspilers array.");

    public static JsonObject SingleStep(Sandbox sandbox) =>
        Assert.IsType<JsonObject>(Assert.Single(Steps(sandbox)));

    public static string RequiredString(JsonObject value, string propertyName) =>
        value[propertyName]?.GetValue<string>()
        ?? throw new InvalidDataException($"Project step has no string '{propertyName}'.");

    public static string ZfsDirectory(Sandbox sandbox, string relativePath = "") =>
        Path.Combine(
            sandbox.ProjectPath,
            ".ssotme",
            relativePath.Trim('/', '\\'));

    public static string[] ZfsFiles(Sandbox sandbox, string relativePath = "")
    {
        var directory = ZfsDirectory(sandbox, relativePath);
        return Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.zfs", SearchOption.TopDirectoryOnly)
            : [];
    }

    public static string SanitizeUrl(string value) =>
        value
            .Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace("?", string.Empty, StringComparison.Ordinal)
            .Replace("&", string.Empty, StringComparison.Ordinal)
            .Replace("=", string.Empty, StringComparison.Ordinal)
            .Replace(" ", "-", StringComparison.Ordinal)
            .ToLowerInvariant();

    public static void WriteMovingIndex(
        Sandbox sandbox,
        MockToolServer server,
        string headVersion,
        string? oldVersion = null)
    {
        var versions = new JsonObject
        {
            [headVersion] = VersionNode(server, headVersion, isHead: true),
        };
        if (oldVersion is not null)
        {
            versions[oldVersion] = VersionNode(server, oldVersion, isHead: false);
        }

        var root = new JsonObject
        {
            ["transpilerVersions"] = new JsonObject
            {
                ["effortless/common/to-uppercase"] = versions,
            },
            ["cliUpdateAvailable"] = false,
            ["latestBridgeVersion"] = null,
            ["latestCliVersion"] = CliUnderTest.PackageVersion,
        };
        sandbox.WriteHomeFile(
            ".ssotme/remote_tools/ssotme-tools.json",
            root.ToJsonString(IndentedJson));
        server.IndexJson = root.ToJsonString(IndentedJson);
    }

    public static async Task<CliResult> RunAlias(
        CliUnderTest cli,
        Sandbox sandbox,
        string alias,
        IReadOnlyList<string> args,
        string cwd,
        int timeoutMs = 120_000)
    {
        var executable = OperatingSystem.IsWindows()
            ? Path.Combine(sandbox.BinPath, alias + ".cmd")
            : Path.Combine(sandbox.BinPath, alias);
        var startInfo = new ProcessStartInfo(executable)
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
        cli.ApplySandboxEnvironment(startInfo, sandbox);

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
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            throw new TimeoutException($"Alias '{alias}' exceeded {timeoutMs} ms.");
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

    public static JsonDocument ReadJsonDocument(string path) =>
        JsonDocument.Parse(File.ReadAllText(path));

    public static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }

    private static JsonObject ToJson(WorkflowStep step)
    {
        var json = new JsonObject
        {
            ["Name"] = step.Name,
            ["RelativePath"] = step.RelativePath,
            ["CommandLine"] = step.CommandLine,
            ["IsDisabled"] = step.IsDisabled,
        };
        if (step.TranspilerGroup is not null)
        {
            json["TranspilerGroup"] = step.TranspilerGroup;
        }
        if (step.PinnedVersion is not null)
        {
            json["PinnedVersion"] = step.PinnedVersion;
        }
        if (step.LastVersionUsed is not null)
        {
            json["LastVersionUsed"] = step.LastVersionUsed;
        }
        if (step.LastUrl is not null)
        {
            json["LastUrl"] = step.LastUrl;
        }
        return json;
    }

    private static JsonObject VersionNode(
        MockToolServer server,
        string version,
        bool isHead) =>
        new()
        {
            ["metaData"] = new JsonObject
            {
                ["isHeadVersion"] = isHead,
            },
            ["urls"] = new JsonObject
            {
                ["post"] = server.ToolUri("to-uppercase", version).ToString(),
            },
        };
}
