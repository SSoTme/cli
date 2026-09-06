namespace Effortless.Cli.E2E.Suites;

public sealed class NetworkTests
{
    [Fact(
        DisplayName = "net-live-bridge-list: live bridge refresh",
        Skip = "Requires a live internet connection to the real remote-tools bridge; excluded from deterministic CI.")]
    [Trait("Slow", "true")]
    public void LiveBridgeRefreshRequiresRealInternet()
    {
    }

    [Fact(
        DisplayName = "net-live-tool-run: live to-uppercase run",
        Skip = "Requires a live internet connection to the real to-uppercase tool; excluded from deterministic CI.")]
    [Trait("Slow", "true")]
    public void LiveToolRunRequiresRealInternet()
    {
    }

    [Fact(
        DisplayName = "net-upgrade-cli-check: upgradeCli against GitHub",
        Skip = "Requires a live internet connection to GitHub releases; excluded from deterministic CI.")]
    [Trait("Slow", "true")]
    public void UpgradeCliCheckRequiresRealInternet()
    {
    }
}
