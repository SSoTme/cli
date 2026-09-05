using Effortless.Cli.Config;

namespace Effortless.Cli.Tests;

public sealed class UserConfigMigrationTests
{
    [Theory(DisplayName = "unit-home-migration-map: legacy relative paths map to their v2 names")]
    [InlineData("ssotme.key", "effortless.key")]
    [InlineData("ssotme.bob.key", "effortless.bob.key")]
    [InlineData("remote_tools/ssotme-tools.json", "remote_tools/effortless-tools.json")]
    [InlineData("remote_tools/ssotme.json", null)]
    [InlineData("MIGRATED-TO-EFFORTLESS", null)]
    [InlineData("remote_tools/cli_version", "remote_tools/cli_version")]
    [InlineData("remote_tools/effortless.json", "remote_tools/effortless.json")]
    [InlineData("tool_urls.json", "tool_urls.json")]
    [InlineData("seed_cache/x/ssotme.key", "seed_cache/x/ssotme.key")]
    [InlineData("ssotme.key.bak", "ssotme.key.bak")]
    public void MapsLegacyPaths(string legacy, string? expected)
    {
        Assert.Equal(expected, UserConfigMigration.MapRelativePath(legacy));
    }

    [Fact(DisplayName = "unit-home-migration-copy: migrate copies with renames, marks the source, and stages atomically")]
    public void MigrateCopiesRenamesAndMarks()
    {
        using var home = new TestDirectory();
        var legacy = Directory.CreateDirectory(home.File(".ssotme"));
        Directory.CreateDirectory(home.File(".ssotme/remote_tools"));
        File.WriteAllText(home.File(".ssotme/ssotme.key"), "{\"APIKeys\":{}}");
        File.WriteAllText(home.File(".ssotme/remote_tools/ssotme-tools.json"), "{}");
        File.WriteAllText(home.File(".ssotme/remote_tools/ssotme.json"), "{}");
        File.WriteAllText(home.File(".ssotme/tool_urls.json"), "{\"a\":\"b\"}");
        var target = new DirectoryInfo(home.File(".effortless"));
        var lines = new List<string>();

        UserConfigMigration.Migrate(legacy, target, lines.Add);

        Assert.True(File.Exists(home.File(".effortless/effortless.key")));
        Assert.True(File.Exists(home.File(".effortless/remote_tools/effortless-tools.json")));
        Assert.True(File.Exists(home.File(".effortless/tool_urls.json")));
        Assert.False(File.Exists(home.File(".effortless/remote_tools/ssotme.json")));
        Assert.False(File.Exists(home.File(".effortless/ssotme.key")));
        Assert.True(File.Exists(home.File(".ssotme/ssotme.key")));
        Assert.True(File.Exists(home.File(".ssotme/MIGRATED-TO-EFFORTLESS")));
        Assert.Empty(Directory.GetDirectories(home.Path, ".effortless.migrating-*"));
        var line = Assert.Single(lines);
        Assert.Contains("(legacy directory left in place).", line, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "unit-home-migration-failure: a blocked target fails cleanly and removes the staging copy")]
    public void MigrateFailureLeavesNothingBehind()
    {
        using var home = new TestDirectory();
        var legacy = Directory.CreateDirectory(home.File(".ssotme"));
        File.WriteAllText(home.File(".ssotme/tool_urls.json"), "{}");
        File.WriteAllText(home.File(".effortless"), "a file, not a directory");
        var target = new DirectoryInfo(home.File(".effortless"));

        var exception = Assert.Throws<UserConfigMigrationException>(
            () => UserConfigMigration.Migrate(legacy, target, _ => { }));

        Assert.Contains("Could not migrate", exception.Message, StringComparison.Ordinal);
        Assert.Contains("nothing was changed", exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetDirectories(home.Path, ".effortless.migrating-*"));
        Assert.False(File.Exists(home.File(".ssotme/MIGRATED-TO-EFFORTLESS")));
        Assert.Equal("a file, not a directory", File.ReadAllText(home.File(".effortless")));
    }
}
