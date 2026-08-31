using Effortless.Cli;

namespace Effortless.Cli.Tests;

public sealed class VersionKeyTests
{
    [Fact(DisplayName = "unit-version-key-parse: version parsing and ordering match legacy")]
    public void VersionParsingAndOrderingMatchLegacy()
    {
        var tool = VersionKey.ParseVersionKey("v2026.04.03.1718");
        var package = VersionKey.ParseCliVersion("2026-04-19.23.12");
        var invalid = VersionKey.ParseVersionKey("garbage");

        Assert.Equal((2026, 4, 3, 1718), tool);
        Assert.Equal((2026, 4, 19, 2312), package);
        Assert.Equal((0, 0, 0, 0), invalid);
        Assert.True(package.CompareTo(tool) > 0);
        Assert.Equal(
            "v2026.04.03.1718",
            VersionKey.ExtractVersionFromUrl(
                "https://host/tool-v2026-04-03-1718.example/"));
        Assert.Null(VersionKey.ExtractVersionFromUrl("https://host/tool/latest/"));
    }
}
