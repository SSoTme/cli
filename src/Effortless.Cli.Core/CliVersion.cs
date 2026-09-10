namespace Effortless.Cli;

public static class CliVersion
{
    public const string Value = "2026.910.1934";

    /// <summary>
    /// Human-unambiguous, zero-padded form of the same UTC instant as
    /// <see cref="Value"/>: <c>v{yyyy}-{MM}-{dd}-{HHmm}</c>. Stamped by
    /// scripts/release.sh alongside Value.
    /// </summary>
    public const string DisplayVersion = "v2026-09-10-1934";

    /// <summary>
    /// The full commit SHA the release was cut from. Stamped by
    /// scripts/release.sh immediately before the release commit.
    /// </summary>
    public const string CommitSha = "e9642a47e46cc10c7573fa35eff9034bedc75e0e";
}
