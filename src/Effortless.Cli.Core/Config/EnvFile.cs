namespace Effortless.Cli.Config;

public class EnvFile
{
    private readonly Dictionary<string, string> _values;

    private static readonly Dictionary<string, string[]> ParamSuffixMap =
        new Dictionary<string, string[]>
        {
            { "apiKey", new[] { "pat", "api_key", "apikey", "key" } },
            { "baseId", new[] { "baseid", "base_id" } },
        };

    private readonly bool _debug;

    private EnvFile(Dictionary<string, string> values, bool debug = false)
    {
        _values = values;
        _debug = debug;
    }

    public static EnvFile LoadFrom(string projectRootPath, bool debug = false)
    {
        var envPath = Path.Combine(projectRootPath, "effortless.env");
        if (!File.Exists(envPath))
        {
            envPath = Path.Combine(projectRootPath, "ssotme.env");
        }

        if (!File.Exists(envPath))
        {
            if (debug)
            {
                Console.WriteLine(
                    $"DEBUG: No effortless.env or ssotme.env found at {projectRootPath}");
            }

            return null;
        }

        if (debug)
        {
            Console.WriteLine($"DEBUG: Loading env file from {envPath}");
        }

        var values = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
            {
                continue;
            }

            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex <= 0)
            {
                continue;
            }

            var key = trimmed.Substring(0, eqIndex).Trim();
            var value = trimmed.Substring(eqIndex + 1).Trim();

            if (value.Length >= 2 &&
                ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                 (value.StartsWith("'") && value.EndsWith("'"))))
            {
                value = value.Substring(1, value.Length - 2);
            }

            if (debug)
            {
                Console.WriteLine(
                    $"DEBUG: env entry: {key}=<{value.Length} chars>");
            }

            values[key] = value;
        }

        if (debug)
        {
            Console.WriteLine(
                $"DEBUG: Loaded {values.Count} entries from env file");
        }

        return new EnvFile(values, debug);
    }

    public static EnvFile TryLoadFromNearestProject(bool debug = false)
    {
        try
        {
            var dir = new DirectoryInfo(Environment.CurrentDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "effortless.json")) ||
                    File.Exists(Path.Combine(dir.FullName, "ssotme.json")) ||
                    File.Exists(Path.Combine(dir.FullName, "aicapture.json")))
                {
                    return LoadFrom(dir.FullName, debug);
                }

                dir = dir.Parent;
            }
        }
        catch
        {
            // If current directory is invalid or inaccessible, return null.
        }

        if (debug)
        {
            Console.WriteLine(
                "DEBUG: No project found when searching for env file");
        }

        return null;
    }

    public string GetValue(string key)
    {
        string value;
        return _values.TryGetValue(key, out value) ? value : null;
    }

    public bool HasKey(string key)
    {
        return _values.ContainsKey(key);
    }

    public Dictionary<string, string> ResolveAccountParams(string accountName)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(accountName))
        {
            return result;
        }

        if (_debug)
        {
            Console.WriteLine(
                $"DEBUG: Resolving env params for account '{accountName}'");
        }

        foreach (var mapping in ParamSuffixMap)
        {
            var paramName = mapping.Key;
            foreach (var suffix in mapping.Value)
            {
                var envKey = accountName + "_" + suffix;
                var value = GetValue(envKey);
                if (!string.IsNullOrEmpty(value))
                {
                    if (_debug)
                    {
                        Console.WriteLine(
                            $"DEBUG: Matched env key '{envKey}' -> param '{paramName}'");
                    }

                    result[paramName] = value;
                    break;
                }

                if (_debug)
                {
                    Console.WriteLine(
                        $"DEBUG: No match for env key '{envKey}'");
                }
            }
        }

        if (_debug)
        {
            Console.WriteLine(
                $"DEBUG: Resolved {result.Count} params from env file for account '{accountName}'");
        }

        return result;
    }

    public static string ReadEnvValue(string envFilePath, string key)
    {
        if (!File.Exists(envFilePath))
        {
            return null;
        }

        foreach (var line in File.ReadAllLines(envFilePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
            {
                continue;
            }

            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex <= 0)
            {
                continue;
            }

            var lineKey = trimmed.Substring(0, eqIndex).Trim();
            if (lineKey.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed.Substring(eqIndex + 1).Trim();
                if (value.Length >= 2 &&
                    ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                     (value.StartsWith("'") && value.EndsWith("'"))))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                return value;
            }
        }

        return null;
    }

    public static void WriteEnvValue(
        string envFilePath,
        string key,
        string value)
    {
        var newLine = $"{key}={value}";
        if (File.Exists(envFilePath))
        {
            var lines = File.ReadAllLines(envFilePath).ToList();
            bool replaced = false;
            for (int i = 0; i < lines.Count; i++)
            {
                var trimmed = lines[i].Trim();
                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex <= 0)
                {
                    continue;
                }

                var lineKey = trimmed.Substring(0, eqIndex).Trim();
                if (lineKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = newLine;
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                lines.Add(newLine);
            }

            File.WriteAllLines(envFilePath, lines);
        }
        else
        {
            File.WriteAllText(
                envFilePath,
                newLine + Environment.NewLine);
        }
    }
}
