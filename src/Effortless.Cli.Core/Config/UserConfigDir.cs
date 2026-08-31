using System.Diagnostics;

namespace Effortless.Cli.Config;

public static class UserConfigDir
{
    public static DirectoryInfo SSoTmeDir
    {
        get
        {
            var ssotmeDir = new DirectoryInfo(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".ssotme"));

            if (!ssotmeDir.Exists)
            {
                ssotmeDir.Create();

                if (Environment.OSVersion.Platform == PlatformID.Unix ||
                    Environment.OSVersion.Platform == PlatformID.MacOSX)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = $"700 \"{ssotmeDir.FullName}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        })?.WaitForExit();
                    }
                    catch
                    {
                        // Silently ignore chmod failures.
                    }
                }
            }

            return ssotmeDir;
        }
    }
}
