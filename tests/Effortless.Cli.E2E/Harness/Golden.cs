using System.Text.RegularExpressions;

namespace Effortless.Cli.E2E.Harness;

internal static partial class Golden
{
    public static string Normalize(string text, Sandbox sandbox, string? mockBaseUrl = null)
    {
        var normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace(sandbox.ProjectPath, "<ROOT>", StringComparison.Ordinal)
            .Replace(sandbox.HomePath, "<HOME>", StringComparison.Ordinal)
            .Replace(CliUnderTest.PackageVersion, "<VER>", StringComparison.Ordinal);

        if (!string.IsNullOrWhiteSpace(mockBaseUrl))
        {
            normalized = normalized.Replace(mockBaseUrl, "<MOCK>", StringComparison.Ordinal);
        }

        normalized = GuidRegex().Replace(normalized, "<GUID>");
        normalized = IsoTimestampRegex().Replace(normalized, "<TIME>");
        return string.Join(
            "\n",
            normalized.Split('\n').Select(line => line.TrimEnd())).TrimEnd() + "\n";
    }

    public static void AssertMatches(string testCaseId, string normalizedOutput)
    {
        var directory = Path.Combine(
            CliUnderTest.Root,
            "tests",
            "Effortless.Cli.E2E",
            "Goldens");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, testCaseId + ".txt");

        if (Environment.GetEnvironmentVariable("EFFORTLESS_RECORD_GOLDENS") == "1")
        {
            File.WriteAllText(path, normalizedOutput);
            return;
        }

        Assert.True(
            File.Exists(path),
            $"Golden does not exist: {path}. Record it with EFFORTLESS_RECORD_GOLDENS=1.");
        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), normalizedOutput);
    }

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z?\b")]
    private static partial Regex IsoTimestampRegex();
}
