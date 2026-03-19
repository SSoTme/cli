using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SSoTme.OST.Lib.SassySDK.Derived
{
    public class SsotmeEnvFile
    {
        private Dictionary<string, string> _values;

        private SsotmeEnvFile(Dictionary<string, string> values)
        {
            _values = values;
        }

        public static SsotmeEnvFile LoadFrom(string projectRootPath)
        {
            var envPath = Path.Combine(projectRootPath, "ssotme.env");
            if (!File.Exists(envPath)) return null;

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

                values[key] = value;
            }

            return new SsotmeEnvFile(values);
        }

        public static SsotmeEnvFile TryLoadFromNearestProject()
        {
            try
            {
                var dir = new DirectoryInfo(Environment.CurrentDirectory);
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "ssotme.json")) ||
                        File.Exists(Path.Combine(dir.FullName, "aicapture.json")))
                    {
                        return LoadFrom(dir.FullName);
                    }
                    dir = dir.Parent;
                }
            }
            catch
            {
                // If current directory is invalid or inaccessible, return null
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

        public string GetAirtableKey()
        {
            return GetValue("AIRTABLE_PAT") ?? GetValue("AIRTABLE_API_KEY");
        }

        public Tuple<string, string> GetBaserowCredentials()
        {
            var username = GetValue("BASEROW_USERNAME");
            var password = GetValue("BASEROW_PASSWORD");
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                return Tuple.Create(username, password);
            return null;
        }

        public string GetAccountKey(string accountName)
        {
            if (string.IsNullOrEmpty(accountName)) return null;
            var name = accountName.ToUpperInvariant();
            return GetValue(name + "_PAT") ?? GetValue(name + "_API_KEY");
        }
    }
}
