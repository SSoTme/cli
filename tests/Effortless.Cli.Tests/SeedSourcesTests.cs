using Effortless.Cli.Seeds;
using Xunit;

namespace Effortless.Cli.Tests;

public sealed class SeedSourcesTests
{
    [Fact(DisplayName = "unit-seed-sources: SeedSources round-trips the file, prepends the env account, and validates names")]
    public void RoundTripsAndValidates()
    {
        using var home = new TestDirectory();
        var path = home.File("seed_sources.json");
        var sources = new SeedSources(path, environmentAccount: null);

        var defaults = sources.Load();
        Assert.Equal(["ssotme", "effortlessapi"], defaults.Select(s => s.Account));
        Assert.All(defaults, s => Assert.True(s.IsDefault));
        Assert.False(File.Exists(path));

        Assert.True(sources.Add("acme"));
        Assert.False(sources.Add("ACME"));
        Assert.True(File.Exists(path));
        Assert.Equal(["ssotme", "effortlessapi", "acme"], sources.LoadStored());
        Assert.All(sources.Load(), s => Assert.False(s.IsDefault));

        Assert.True(sources.Remove("ssotme"));
        Assert.False(sources.Remove("ssotme"));
        Assert.Equal(["effortlessapi", "acme"], new SeedSources(path, environmentAccount: null).LoadStored());

        var withEnv = new SeedSources(path, environmentAccount: "extra").Load();
        Assert.Equal(["extra", "effortlessapi", "acme"], withEnv.Select(s => s.Account));
        Assert.True(withEnv[0].IsFromEnvironment);
        Assert.Equal(" (env)", withEnv[0].Marker);
        Assert.Equal(["effortlessapi", "acme"], sources.LoadStored());

        Assert.Throws<ArgumentException>(() => sources.Add("bad/name"));
        Assert.Throws<ArgumentException>(() => sources.Remove(""));
        Assert.False(SeedSources.IsValidAccount("-leading"));
        Assert.True(SeedSources.IsValidAccount("effortless-api"));
    }
}
