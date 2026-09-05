namespace Effortless.Cli.Config;

public static class UserConfigDir
{
    /// <summary>
    /// <c>~/.effortless</c>, created on first access (mode 700 on Unix). A legacy
    /// <c>~/.ssotme</c> is migrated first by <see cref="UserConfigMigration"/>.
    /// </summary>
    public static DirectoryInfo EffortlessDir
    {
        get
        {
            UserConfigMigration.EnsureMigrated();
            var directory = new DirectoryInfo(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    UserConfigMigration.EffortlessDirName));

            if (!directory.Exists)
            {
                directory.Create();
                UserConfigMigration.SetUnixMode(directory.FullName, "700");
            }

            return directory;
        }
    }
}
