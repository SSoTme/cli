using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Effortless.Cli.Tests;

public sealed class DevopsGenerationTests
{
    [Fact(DisplayName = "devops-generation-drift: generated files match the rulebook")]
    public async Task GeneratedFilesMatchTheRulebook()
    {
        var result = await RunNode("scripts/generate-from-rulebook.mjs", "--check");

        Assert.True(
            result.ExitCode == 0,
            $"Generator drift detected.{Environment.NewLine}{result.StandardOutput}{result.StandardError}");
    }

    [Fact(DisplayName = "devops-coverage-gate: every retained option has a test")]
    public void EveryRetainedOptionHasATest()
    {
        using var rulebook = LoadRulebook();
        var root = rulebook.RootElement;
        var retainedDispositions = root.GetProperty("Dispositions")
            .GetProperty("data")
            .EnumerateArray()
            .Where(row => row.GetProperty("IsRetained").GetBoolean())
            .Select(row => row.GetProperty("DispositionId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var testCounts = root.GetProperty("TestCases")
            .GetProperty("data")
            .EnumerateArray()
            .GroupBy(row => row.GetProperty("PrimaryOption").GetString()!)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var untested = root.GetProperty("CliOptions")
            .GetProperty("data")
            .EnumerateArray()
            .Where(row =>
                retainedDispositions.Contains(
                    row.GetProperty("Disposition").GetString()!))
            .Select(row => row.GetProperty("CliOptionId").GetString()!)
            .Where(option => !testCounts.TryGetValue(option, out var count) || count == 0)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            untested.Length == 0,
            $"Retained options without test cases:{Environment.NewLine}{string.Join(Environment.NewLine, untested)}");
    }

    [Fact(DisplayName = "devops-test-manifest: every required manifest case exists in code")]
    public void EveryRequiredManifestCaseExistsInCode()
    {
        var required = LoadManifest(
                "tests/Effortless.Cli.E2E/TestManifest.g.json")
            .Concat(
                LoadManifest(
                    "tests/Effortless.Cli.Tests/TestManifest.g.json"))
            .Where(entry => entry.Priority is "P0" or "P1")
            .Select(entry => entry.Id)
            .ToHashSet(StringComparer.Ordinal);
        var assemblies = new[]
        {
            Assembly.GetExecutingAssembly(),
            typeof(Effortless.Cli.E2E.Suites.ManifestCoverageTests).Assembly,
        };
        var implemented = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetMethods())
            .SelectMany(method =>
                method.GetCustomAttributes<FactAttribute>()
                    .Select(attribute => attribute.DisplayName))
            .Where(displayName => !string.IsNullOrWhiteSpace(displayName))
            .Select(displayName => displayName!.Split(':', 2)[0])
            .ToHashSet(StringComparer.Ordinal);
        var missing = required
            .Except(implemented)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"Missing P0/P1 manifest tests:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }

    private static JsonDocument LoadRulebook()
    {
        return JsonDocument.Parse(
            File.ReadAllText(
                Path.Combine(
                    RepositoryRoot,
                    "effortless-rulebook",
                    "effortless-rulebook.json")));
    }

    private static IReadOnlyList<ManifestEntry> LoadManifest(string relativePath)
    {
        return JsonSerializer.Deserialize<List<ManifestEntry>>(
                   File.ReadAllText(Path.Combine(RepositoryRoot, relativePath)),
                   new JsonSerializerOptions
                   {
                       PropertyNameCaseInsensitive = true,
                   })
               ?? throw new InvalidOperationException(
                   $"Could not parse generated manifest '{relativePath}'.");
    }

    private static async Task<ProcessResult> RunNode(
        string script,
        params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("node")
            {
                WorkingDirectory = RepositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add(script);
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start Node.js.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new ProcessResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(
                        Path.Combine(
                            directory.FullName,
                            "effortless-rulebook",
                            "effortless-rulebook.json")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the effortless-cli repository root.");
        }
    }

    private sealed record ManifestEntry(
        string Id,
        string Suite,
        string Priority,
        string Status,
        bool Slow,
        bool Interactive);

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
