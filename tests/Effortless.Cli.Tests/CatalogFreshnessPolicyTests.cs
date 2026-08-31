using Effortless.Cli;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli.Tests;

public sealed class CatalogFreshnessPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 31, 6, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "unit-catalog-freshness: timestamp boundaries use the injected clock")]
    public void TimestampBoundariesUseInjectedClock()
    {
        var policy = new CatalogFreshnessPolicy(
            new FixedTimeProvider(Now));

        Assert.True(
            policy.IsFresh(
                Catalog(Now - TimeSpan.FromHours(24)
                    + TimeSpan.FromSeconds(1))));
        Assert.False(
            policy.IsFresh(
                Catalog(Now - TimeSpan.FromHours(24))));
        Assert.False(policy.IsFresh(Catalog("not-a-time")));
        Assert.True(
            policy.IsFresh(
                Catalog(Now + TimeSpan.FromMinutes(5))));
        Assert.False(
            policy.IsFresh(
                Catalog(Now + TimeSpan.FromMinutes(5)
                    + TimeSpan.FromTicks(1))));
        Assert.False(policy.IsFresh(new JObject()));
    }

    [Fact]
    public void RemoteIndexAcceptsFreshTimestamp()
    {
        using var directory = new TestDirectory();
        var index = new RemoteToolsIndex(
            new DirectoryInfo(directory.Path),
            refreshRunner: _ => false,
            writeLine: _ => { },
            validateHost: _ => { },
            timeProvider: new FixedTimeProvider(Now));
        Directory.CreateDirectory(
            index.RemoteToolsDirectory.FullName);
        File.WriteAllText(
            index.IndexFile.FullName,
            new JObject
            {
                ["transpilerVersions"] = new JObject
                {
                    ["effortless/common/echo"] =
                        new JObject(),
                },
                ["fetchedAt"] =
                    (Now - TimeSpan.FromHours(24)
                     + TimeSpan.FromSeconds(1))
                    .ToString("O"),
            }.ToString());

        Assert.False(index.IsEmpty);
        Assert.True(
            new CatalogFreshnessPolicy(
                new FixedTimeProvider(Now))
            .IsFresh(index.RawRoot),
            index.RawRoot.ToString());
        Assert.True(
            index.EnsureFresh(),
            index.LastRefreshError);
    }

    private static JObject Catalog(DateTimeOffset fetchedAt) =>
        Catalog(fetchedAt.ToString("O"));

    private static JObject Catalog(string fetchedAt) =>
        new()
        {
            ["fetchedAt"] = fetchedAt,
        };

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() =>
            _utcNow;
    }
}
