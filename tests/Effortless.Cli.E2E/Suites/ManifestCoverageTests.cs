using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Effortless.Cli.E2E.Suites;

public sealed class ManifestCoverageTests
{
    [Fact(DisplayName = "meta-manifest-coverage: every required rulebook case has a test")]
    public void EveryRequiredRulebookCaseHasATest()
    {
        using var rulebook = JsonDocument.Parse(
            File.ReadAllText(
                Path.Combine(
                    Harness.CliUnderTest.Root,
                    "effortless-rulebook",
                    "effortless-rulebook.json")));
        var root = rulebook.RootElement;
        var blackBoxSuites = root.GetProperty("TestSuites").GetProperty("data")
            .EnumerateArray()
            .Where(row => row.GetProperty("Kind").GetString() == "black-box")
            .Select(row => row.GetProperty("TestSuiteId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var required = root.GetProperty("TestCases").GetProperty("data")
            .EnumerateArray()
            .Where(row => blackBoxSuites.Contains(row.GetProperty("Suite").GetString()!))
            .Where(row =>
                row.GetProperty("Priority").GetString() is "P0" or "P1"
                && row.GetProperty("Status").GetString()
                    is not "deleted")
            .Select(row => row.GetProperty("TestCaseId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var implemented = Assembly.GetExecutingAssembly()
            .GetTypes()
            .SelectMany(type => type.GetMethods())
            .SelectMany(method =>
                method.GetCustomAttributes<FactAttribute>()
                    .Select(attribute => attribute.DisplayName))
            .Where(displayName => !string.IsNullOrWhiteSpace(displayName))
            .Select(displayName => displayName!.Split(':', 2)[0])
            .ToHashSet(StringComparer.Ordinal);

        var missing = required.Except(implemented).Order(StringComparer.Ordinal).ToArray();

        Assert.True(
            missing.Length == 0,
            $"Missing P0/P1 black-box tests:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }
}
