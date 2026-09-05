using System.Diagnostics;

namespace Effortless.Cli.Config;

/// <summary>
/// One-time, copy-only migration of the legacy <c>~/.ssotme</c> user state
/// directory to <c>~/.effortless</c> (step 10, D28/D31). This is the only
/// module allowed to know the legacy names. The legacy directory is never
/// modified beyond a marker file so an older <c>ssotme</c> binary keeps working
/// alongside the new CLI.
/// </summary>
public static class UserConfigMigration
{
    public const string EffortlessDirName = ".effortless";
    public const string LegacyDirName = ".ssotme";
    public const string MarkerFileName = "MIGRATED-TO-EFFORTLESS";

    private const string LegacyKeyFileName = "ssotme.key";
    private const string LegacyKeyFilePrefix = "ssotme.";
    private const string LegacyKeyFileSuffix = ".key";
    private const string LegacyRemoteToolsDir = "remote_tools";
    private const string LegacyIndexFileName = "ssotme-tools.json";
    private const string LegacyRemoteToolsProjectFileName = "ssotme.json";

    private static readonly object Gate = new();
    private static bool _checked;

    /// <summary>
    /// Runs the migration at most once per process. Safe to call from any
    /// code path that is about to touch the user configuration directory.
    /// Migration failure is fatal: the CLI never runs half-migrated.
    /// </summary>
    public static void EnsureMigrated(Action<string> writeLine = null)
    {
        lock (Gate)
        {
            if (_checked)
            {
                return;
            }

            _checked = true;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var target = new DirectoryInfo(Path.Combine(home, EffortlessDirName));
        var legacy = new DirectoryInfo(Path.Combine(home, LegacyDirName));
        if (target.Exists || !legacy.Exists)
        {
            return;
        }

        Migrate(legacy, target, writeLine ?? Console.Error.WriteLine);
    }

    /// <summary>
    /// Copies <paramref name="legacy"/> to <paramref name="target"/> with the
    /// file renames of step 10, staging into a sibling temp directory so a
    /// partial copy is never observable as <c>~/.effortless</c>.
    /// </summary>
    public static void Migrate(
        DirectoryInfo legacy,
        DirectoryInfo target,
        Action<string> writeLine)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(writeLine);

        var staging = new DirectoryInfo(
            Path.Combine(
                target.Parent!.FullName,
                $"{target.Name}.migrating-{Guid.NewGuid():N}"));
        try
        {
            staging.Create();
            foreach (var file in legacy.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(legacy.FullName, file.FullName);
                var mapped = MapRelativePath(relative);
                if (mapped == null)
                {
                    continue;
                }

                var destination = new FileInfo(Path.Combine(staging.FullName, mapped));
                destination.Directory!.Create();
                file.CopyTo(destination.FullName, overwrite: false);
            }

            staging.MoveTo(target.FullName);
            SetUnixMode(target.FullName, "700");
            foreach (var keyFile in target.EnumerateFiles("effortless*.key"))
            {
                SetUnixMode(keyFile.FullName, "600");
            }

            File.WriteAllText(
                Path.Combine(legacy.FullName, MarkerFileName),
                $"Migrated to {target.FullName} at {DateTime.UtcNow:O} by Effortless CLI {CliVersion.Value}{Environment.NewLine}");
        }
        catch (Exception exception)
        {
            TryDelete(staging);
            throw new UserConfigMigrationException(
                $"Could not migrate {Tilde(legacy)} to {Tilde(target)}: {exception.Message} " +
                $"Fix the problem (or move {Tilde(legacy)} aside) and run the command again; nothing was changed.",
                exception);
        }

        writeLine(
            $"Migrated {Tilde(legacy)} to {Tilde(target)} (legacy directory left in place).");
    }

    /// <summary>
    /// Maps a legacy-relative path to its v2-relative path, or null when the
    /// file is dropped.
    /// </summary>
    public static string MapRelativePath(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var normalized = relativePath.Replace('\\', '/');
        var directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/') ?? string.Empty;
        var name = Path.GetFileName(normalized);

        if (directory.Length == 0)
        {
            if (name == LegacyKeyFileName)
            {
                return "effortless.key";
            }

            if (name.StartsWith(LegacyKeyFilePrefix, StringComparison.Ordinal)
                && name.EndsWith(LegacyKeyFileSuffix, StringComparison.Ordinal)
                && name.Length > LegacyKeyFilePrefix.Length + LegacyKeyFileSuffix.Length)
            {
                return "effortless." + name[LegacyKeyFilePrefix.Length..];
            }

            if (name == MarkerFileName)
            {
                return null;
            }
        }

        if (directory == LegacyRemoteToolsDir)
        {
            if (name == LegacyIndexFileName)
            {
                return LegacyRemoteToolsDir + "/effortless-tools.json";
            }

            if (name == LegacyRemoteToolsProjectFileName)
            {
                return null;
            }
        }

        return normalized;
    }

    private static string Tilde(DirectoryInfo directory)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = directory.FullName;
        return full.StartsWith(home, StringComparison.Ordinal)
            ? "~" + full[home.Length..].Replace('\\', '/')
            : full;
    }

    private static void TryDelete(DirectoryInfo directory)
    {
        try
        {
            directory.Refresh();
            if (directory.Exists)
            {
                directory.Delete(recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup of the staging directory.
        }
    }

    internal static void SetUnixMode(string path, string mode)
    {
        if (Environment.OSVersion.Platform != PlatformID.Unix
            && Environment.OSVersion.Platform != PlatformID.MacOSX)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"{mode} \"{path}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            })?.WaitForExit();
        }
        catch
        {
            // Silently ignore chmod failures.
        }
    }
}

public sealed class UserConfigMigrationException : Exception
{
    public UserConfigMigrationException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
