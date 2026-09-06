using System.Text.RegularExpressions;

namespace Effortless.Cli;

public static class VersionKey
{
    // Accepts both v2026.04.03.1718 and 2026-04-19.23.12.
    public static (int, int, int, int) ParseVersionKey(string key, bool debug = false)
    {
        try
        {
            var s = key.TrimStart('v').Replace('-', '.');
            var parts = s.Split('.');
            if (parts.Length == 4 &&
                int.TryParse(parts[0], out int y) &&
                int.TryParse(parts[1], out int mo) &&
                int.TryParse(parts[2], out int d) &&
                int.TryParse(parts[3], out int t))
            {
                return (y, mo, d, t);
            }

            if (parts.Length == 5 &&
                int.TryParse(parts[0], out int y5) &&
                int.TryParse(parts[1], out int mo5) &&
                int.TryParse(parts[2], out int d5) &&
                int.TryParse(parts[3], out int h5) &&
                int.TryParse(parts[4], out int m5))
            {
                return (y5, mo5, d5, h5 * 100 + m5);
            }
        }
        catch
        {
            if (debug)
            {
                System.Console.ForegroundColor = ConsoleColor.Yellow;
                System.Console.WriteLine($"Could not parse version: {key}");
                System.Console.ResetColor();
            }
        }

        return (0, 0, 0, 0);
    }

    public static (int, int, int, int) ParseCliVersion(string version)
    {
        try
        {
            var s = (version ?? "").TrimStart('v');
            var m = Regex.Match(s, @"^(\d{4})-(\d{2})-(\d{2})\.(\d{1,2})\.(\d{2})$");
            if (m.Success)
            {
                int hhmm = int.Parse(m.Groups[4].Value) * 100 + int.Parse(m.Groups[5].Value);
                return (
                    int.Parse(m.Groups[1].Value),
                    int.Parse(m.Groups[2].Value),
                    int.Parse(m.Groups[3].Value),
                    hhmm);
            }

            return ParseVersionKey(s, false);
        }
        catch
        {
            return (0, 0, 0, 0);
        }
    }

    public static string ExtractVersionFromUrl(string url)
    {
        try
        {
            var regex = new Regex(@"-v(\d{4}-\d{2}-\d{2}-\d{4})");
            var match = regex.Match(url);

            if (match.Success)
            {
                var versionPart = match.Groups[1].Value;
                var parts = versionPart.Split('-');
                if (parts.Length == 4)
                {
                    return $"v{parts[0]}.{parts[1]}.{parts[2]}.{parts[3]}";
                }
            }
        }
        catch (Exception ex)
        {
            System.Console.ForegroundColor = ConsoleColor.Yellow;
            System.Console.WriteLine(
                $"  Warning: Could not extract version from URL: {ex.Message}");
            System.Console.ForegroundColor = ConsoleColor.Gray;
        }

        return null;
    }
}
