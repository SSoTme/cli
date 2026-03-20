using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SSoTme.OST.Lib.SassySDK.Derived
{
    public class SsotmeEnvFile
    {
        private Dictionary<string, string> _values;

        // Maps a case-exact parameter name to case-insensitive env suffixes.
        // When -account xxx is used, looks for {ACCOUNT}_{suffix} in the env file
        // and injects as {paramName}={value} into the CLI parameters.
        private static readonly Dictionary<string, string[]> ParamSuffixMap = new Dictionary<string, string[]>
        {
            { "apiKey",   new[] { "pat", "api_key", "apikey", "key" } },
            { "baseId",   new[] { "baseid", "base_id" } },
        };

        private bool _debug;

        private SsotmeEnvFile(Dictionary<string, string> values, bool debug = false)
        {
            _values = values;
            _debug = debug;
        }

        public static SsotmeEnvFile LoadFrom(string projectRootPath, bool debug = false)
        {
            // Prefer effortless.env, fall back to ssotme.env
            var envPath = Path.Combine(projectRootPath, "effortless.env");
            if (!File.Exists(envPath))
            {
                envPath = Path.Combine(projectRootPath, "ssotme.env");
            }
            if (!File.Exists(envPath))
            {
                if (debug) Console.WriteLine($"DEBUG: No effortless.env or ssotme.env found at {projectRootPath}");
                return null;
            }

            if (debug) Console.WriteLine($"DEBUG: Loading env file from {envPath}");

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex <= 0) continue;

                var key = trimmed.Substring(0, eqIndex).Trim();
                var value = trimmed.Substring(eqIndex + 1).Trim();

                // Strip surrounding quotes
                if (value.Length >= 2 &&
                    ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                     (value.StartsWith("'") && value.EndsWith("'"))))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                if (debug) Console.WriteLine($"DEBUG: env entry: {key}=<{value.Length} chars>");
                values[key] = value;
            }

            if (debug) Console.WriteLine($"DEBUG: Loaded {values.Count} entries from env file");
            return new SsotmeEnvFile(values, debug);
        }

        public static SsotmeEnvFile TryLoadFromNearestProject(bool debug = false)
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
                // If current directory is invalid or inaccessible, return null
            }
            if (debug) Console.WriteLine("DEBUG: No project found when searching for env file");
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

        /// <summary>
        /// Resolves all matching env entries for a given account name using ParamSuffixMap.
        /// For each mapping {paramName -> [suffixes]}, looks for {account}_{suffix} in the env file.
        /// Returns a dictionary of paramName -> value for all matches found.
        /// </summary>
        public Dictionary<string, string> ResolveAccountParams(string accountName)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(accountName)) return result;

            if (_debug) Console.WriteLine($"DEBUG: Resolving env params for account '{accountName}'");

            foreach (var mapping in ParamSuffixMap)
            {
                var paramName = mapping.Key;
                foreach (var suffix in mapping.Value)
                {
                    // Build the env key: {ACCOUNT}_{SUFFIX} — lookup is case-insensitive
                    var envKey = accountName + "_" + suffix;
                    var value = GetValue(envKey);
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (_debug) Console.WriteLine($"DEBUG: Matched env key '{envKey}' -> param '{paramName}'");
                        result[paramName] = value;
                        break; // first matching suffix wins for this param
                    }
                    else if (_debug)
                    {
                        Console.WriteLine($"DEBUG: No match for env key '{envKey}'");
                    }
                }
            }

            if (_debug) Console.WriteLine($"DEBUG: Resolved {result.Count} params from env file for account '{accountName}'");
            return result;
        }
    }
}
