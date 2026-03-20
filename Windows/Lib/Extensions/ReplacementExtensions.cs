using Newtonsoft.Json.Linq;
using SSoTme.OST.Lib.SassySDK.Derived;
using System;
using System.Buffers.Text;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using SSoTme.OST.Lib.Extensions;


namespace SSoTme.OST.Core.Lib.Extensions
{
    public static class ReplacementExtensions
    {

        const string C_HINT_FIELD = "airtable-hint";

        private static FileInfo templatePath = default;
        private static FileInfo ConfigValuesPath = default;
        private static FileInfo SecretValuesPath = default;

        public static FileInfo TemplatePath
        {
            get => templatePath;
            set
            {
                templatePath = value;
                ConfigValuesPath = new FileInfo(Path.Combine(templatePath.Directory.FullName, "seed-config-values.json"));
                if (!ConfigValuesPath.Exists) File.WriteAllText(ConfigValuesPath.FullName, "{}");
                SecretValuesPath = new FileInfo(Path.Combine(templatePath.Directory.FullName, "seed-secrets-values.json"));
                if (!SecretValuesPath.Exists) File.WriteAllText(SecretValuesPath.FullName, "{}");

            }
        }

        public static async Task ApplySeedReplacementsAsync(this DirectoryInfo rootPath, bool reverseApply = false)
        {
            var seedFile = new FileInfo(Path.Combine(rootPath.FullName, "ssotme-seed.json"));
            if (!seedFile.Exists) return;

            TemplatePath = seedFile;
            await CreateSeedConfigAndSecretValues();
            await rootPath.UpdateConfigValuesInRepoFiles();
        }

        private static async Task UpdateConfigValuesInRepoFiles(this DirectoryInfo rootPath)
        {
            var start = DateTime.Now;
            // Assuming replacements are loaded into a JObject somehow; ensure this happens
            JArray replacements = await rootPath.LoadReplacementsAsync("seed-config-values.json") ?? new JArray();  // Placeholder for actual replacement loading logic
            // Now call the updated ReplaceInFiles
            await rootPath.ReplaceInFiles(replacements);

            JArray secretReplacements = await rootPath.LoadReplacementsAsync("seed-secret-values.json") ?? new JArray();
            // Now call the updated ReplaceInFiles
            await rootPath.ReplaceInFiles(secretReplacements);
            var elapsed = DateTime.Now.Subtract(start).TotalMilliseconds;
            // Console.WriteLine($"UPDATING CONFIG VALUES TIME ELAPSED: {elapsed}ms");
        }

        private static async Task<JArray> LoadReplacementsAsync(this DirectoryInfo rootPath, string replacementsFile)
        {
            var seedFilePath = Path.Combine(rootPath.FullName, replacementsFile);
            if (!File.Exists(seedFilePath)) return null;

            var json = File.ReadAllText(seedFilePath);
            JObject seedDetails = JObject.Parse(json);
            JArray replacements = (JArray)seedDetails["replacements"];

            return replacements;
        }


        private static async Task ReplaceInFiles(this DirectoryInfo diToSearch, JArray replacements)
        {
            // Skip the directory if it's node_modules or any other specified
            if (diToSearch.Name.StartsWith(".")) return;
            var ignores = new string[] { "bin", "obj", "node_modules" };
            if (ignores.Any(ignoredPath => diToSearch.Name.Equals(ignoredPath, StringComparison.OrdinalIgnoreCase)))
                return;
            // Process each file in the current directory
            foreach (FileInfo file in diToSearch.GetFiles())
            {
                if (file.Name.StartsWith("."))
                    continue; // Skip hidden files or files starting with '.'

                await file.ProcessFile(replacements);
            }

            // Recursively process each subdirectory
            foreach (DirectoryInfo subDirectory in diToSearch.GetDirectories())
            {
                await ReplaceInFiles(subDirectory, replacements);
            }
        }

        private static async Task ProcessFile(this FileInfo file, JArray replacements)
        {
            if (file.Length > 100000) return;
            string content = await File.ReadAllTextAsync(file.FullName);
            bool fileModified = false;
            string originalFileName = file.Name;

            // Iterate through each replacement
            foreach (var replacement in replacements)
            {
                var key = replacement["key"].ToString();
                var value = replacement["value"].ToString();
                string token = $"${key}$"; // Assuming tokens are wrapped in $ signs

                // Content replacement
                if (content.Contains(token))
                {
                    content = Regex.Replace(content, Regex.Escape(token), value.Replace("$", "\\$"), RegexOptions.IgnoreCase);
                    fileModified = true;
                }

                // File name replacement
                if (originalFileName.Contains(token))
                {
                    string newFileName = Regex.Replace(originalFileName, Regex.Escape(token), value.Replace("$", "\\$"), RegexOptions.IgnoreCase);
                    string newPath = Path.Combine(file.Directory.FullName, newFileName);
                    file.MoveTo(newPath);

                    // Update the file reference if the name changes
                    file = new FileInfo(newPath);
                }
            }

            // If modifications were made, write back to the file
            if (fileModified)
            {
                await File.WriteAllTextAsync(file.FullName, content);
            }
        }

        private static async Task CreateSeedConfigAndSecretValues()
        {
            var seedJson = File.ReadAllText(TemplatePath.FullName);
            var parentConfigPath = Path.Combine(TemplatePath.Directory.Parent.FullName, "seed-config-values.json");
            var parentConfigValuesJson = File.Exists(parentConfigPath) ? File.ReadAllText(parentConfigPath) : "{\"Replacements\":[]}";
            JObject seedDetails = JObject.Parse(seedJson);
            JObject parentConfigValues = JObject.Parse(parentConfigValuesJson);
            (JObject configValues, JObject secretValues) = LoadConfigAndSecretValues();

            bool allValuesProvided = true;
            JObject airtableSchema = null;

            if (seedDetails["replacements"] is null) return;

            foreach (var replacement in seedDetails["replacements"])
            {
                var key = (string)replacement["key"];
                var isSecret = replacement["secret"] is null ? false : (bool)replacement["secret"];
                var description = (string)replacement["description"];

                airtableSchema = await seedDetails.CheckForAirtableHints(configValues, airtableSchema);
                var defaultValue = (isSecret ? secretValues : configValues).GetDefaultValue(key, replacement, parentConfigValues);
                var currentValue = (isSecret ? secretValues : configValues).GetCurrentValue(key);
                var skipValue = (isSecret ? secretValues : configValues).SkipValue(key);
                // If the current value is still null or empty, user interaction is required.
                if (string.IsNullOrEmpty(currentValue) && !skipValue)
                {
                    allValuesProvided = false;

                    Console.WriteLine($"{description}\nNo value found. Enter new text or press ENTER to use the default: {defaultValue}");
                    var userInput = Console.ReadLine();

                    currentValue = !string.IsNullOrEmpty(userInput) ? userInput : defaultValue;

                    if (isSecret)
                    {
                        AddOrUpdateSecret(secretValues, key, currentValue);
                    }
                    else
                    {
                        AddOrUpdateResponse(configValues, key, currentValue);
                    }
                }
            }

            if (!allValuesProvided)
            {
                // Save secrets and responses if any value was missing and has now been populated.
                File.WriteAllText(SecretValuesPath.FullName, secretValues.ToString());
                File.WriteAllText(ConfigValuesPath.FullName, configValues.ToString());
            }
        }

        private static (JObject, JObject) LoadConfigAndSecretValues()
        {
            JObject configValues = File.Exists(ConfigValuesPath.FullName) ? JObject.Parse(File.ReadAllText(ConfigValuesPath.FullName)) : new JObject { ["replacements"] = new JArray() };
            JObject secretValues = File.Exists(SecretValuesPath.FullName) ? JObject.Parse(File.ReadAllText(SecretValuesPath.FullName)) : new JObject { ["replacements"] = new JArray() };
            return (configValues, secretValues);
        }

        private static async Task<JObject> CheckForAirtableHints(this JObject seedDetails, JObject responses, JObject airtableSchema)
        {
            try
            {
                // find BaseId
                var baseId = FindBaseId(responses);
                if (string.IsNullOrEmpty(baseId)) return airtableSchema;

                if (airtableSchema is null) airtableSchema = await GetAirtableSchemaFromBaseId(baseId);

                var responseValues = responses["replacements"] as JArray;

                foreach (var replacement in seedDetails["replacements"])
                {
                    var key = $"{replacement["key"]}";

                    var currentKey = responseValues.FirstOrDefault(fod => $"{fod["key"]}" == $"{key}");

                    if (currentKey is null)
                    {
                        currentKey = JObject.FromObject(new { key = key });
                        responseValues.Add(currentKey);
                    }

                    var currentDefault = replacement["default"];
                    var lookupValue = LookupAirtableValueFromHint($"{key}", $"{currentDefault}", airtableSchema);

                    replacement["default"] = lookupValue;
                }

                responses["replacements"] = responseValues;
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return airtableSchema;
        }

        private static string LookupAirtableValueFromHint(string hint, string currentDefault, JObject airtableSchema)
        {
            switch ($"{hint}")
            {
                case "project-name":
                    return GetAirtableProjectName(currentDefault, airtableSchema);

                case "project-name-title":
                    return GetAirtableProjectNameTitle(currentDefault, airtableSchema);

                case "view":
                    return GetAirtableView(currentDefault, airtableSchema);

                case "user-table-name":
                    return GetAirtableUserTableName(currentDefault, airtableSchema);

                case "users-table-name":
                    return GetAirtableUsersTableName(currentDefault, airtableSchema);

                case "email-address-field":
                    return GetAirtableEmailAddressField(currentDefault, airtableSchema);

                case "roles-field":
                    return GetAirtableRolesField(currentDefault, airtableSchema);

                case "roles":
                    return GetAirtableRoles(currentDefault, airtableSchema);

                case "user-role-name":
                    return GetAirtableUserRoleName(currentDefault, airtableSchema);

                default:
                    return $"{currentDefault}";
            }
        }

        private static string GetAirtableProjectName(string currentDefault, JObject airtableSchema)
        {
            // find the name of the project
            return $"{airtableSchema["name"]}".ToLower().Replace(" ", "-");
        }

        private static string GetAirtableProjectNameTitle(string currentDefault, JObject airtableSchema)
        {
            return $"{airtableSchema["name"]}";
        }

        private static string GetAirtableView(string currentDefault, JObject airtableSchema)
        {
            var tableViews = new Dictionary<string, List<string>>();
            (airtableSchema["tables"] as JArray).ToList().ForEach(table => tableViews[$"{table["name"]}"] = (table["views"] as JArray).Select(view => $"{view["name"]}").ToList());
            var viewList = tableViews.SelectMany(list => list.Value).Distinct();
            var sharedViews = viewList.Where(where => tableViews.All(kvp => kvp.Value.Contains(where)));
            if (!sharedViews.Any()) throw new Exception("There is does not appear to be any common views.");
            var exportView = sharedViews.FirstOrDefault(fod => fod.Contains("export", StringComparison.OrdinalIgnoreCase));
            var nonGridViewView = sharedViews.FirstOrDefault(fod => !fod.Contains("Grid view"));
            var finalView = exportView ?? nonGridViewView ?? sharedViews.First();
            return finalView.Replace(" ", "%20");
        }
        private static string GetAirtableUserTableName(string currentDefault, JObject airtableSchema)
        {
            string singularTableName = GetSingularTableName(airtableSchema);
            return singularTableName;
        }

        private static string GetAirtableUsersTableName(string currentDefault, JObject airtableSchema)
        {
            string singularTableName = GetSingularTableName(airtableSchema);
            if (string.IsNullOrEmpty(singularTableName)) return null;
            return singularTableName.Pluralize();
        }

        private static string GetSingularTableName(JObject airtableSchema)
        {
            string originalTableName = GetUsersTableName(airtableSchema);
            if (String.IsNullOrEmpty(originalTableName)) return string.Empty;
            if (string.IsNullOrEmpty(originalTableName))
                throw new Exception("User table not found in the schema.");

            return originalTableName.Singularize();
        }

        private static string GetUsersTableName(JObject airtableSchema)
        {
            if (airtableSchema == null)
                throw new ArgumentNullException(nameof(airtableSchema));

            var tablesToken = airtableSchema["tables"] as JArray;
            if (tablesToken == null)
                throw new Exception("The schema does not contain a 'tables' array.");

            var tableNames = tablesToken
                .Select(table => $"{table["name"]}")
                .ToList();

            var userTable = tableNames
                .FirstOrDefault(fod => fod.Contains("user", StringComparison.OrdinalIgnoreCase));

            var accountTable = tableNames
                .FirstOrDefault(fod => fod.Contains("account", StringComparison.OrdinalIgnoreCase));

            var originalTableName = $"{userTable ?? accountTable}";

            if (string.IsNullOrEmpty(originalTableName))
            {
                // Fall back logic: Look for any table with fields that include "email" and "role"
                foreach (var table in tablesToken)
                {
                    var fields = table["fields"] as JArray;
                    if (fields == null)
                        continue;

                    var fieldNames = fields
                        .Select(field => $"{field["name"]}".ToLowerInvariant())
                        .ToList();

                    if (fieldNames.Any(fn => fn.Contains("email")) && fieldNames.Any(fn => fn.Contains("role")))
                    {
                        originalTableName = $"{table["name"]}";
                        break;
                    }
                }

                // If no such table is found, return null
                if (string.IsNullOrEmpty(originalTableName))
                    return null;
            }

            return originalTableName;
        }

        private static string GetAirtableEmailAddressField(string currentDefault, JObject airtableSchema)
        {
            var originalTableName = GetUsersTableName(airtableSchema);
            if (String.IsNullOrEmpty(originalTableName)) return null;
            if (string.IsNullOrEmpty(originalTableName))
                throw new Exception("User table not found in the schema.");

            var tablesArray = airtableSchema["tables"] as JArray;
            if (tablesArray == null)
                throw new Exception("The schema does not contain a 'tables' array.");

            var table = tablesArray
                .FirstOrDefault(fod => $"{fod["name"]}" == originalTableName);

            if (table == null)
                throw new Exception($"Table '{originalTableName}' not found in the schema.");

            var fields = table["fields"] as JArray;
            if (fields == null)
                throw new Exception($"Fields not found in table '{originalTableName}'.");

            var emailField = fields
                .FirstOrDefault(fod => $"{fod["name"]}".Contains("email", StringComparison.OrdinalIgnoreCase));

            if (emailField == null)
                throw new Exception("Email field not found in the user table.");

            var emailFieldName = $"{emailField["name"]}";
            return emailFieldName;
        }

        private static string GetAirtableRolesField(string currentDefault, JObject airtableSchema)
        {
            JToken rolesField = GetRolesField(airtableSchema);
            if (rolesField is null) return null;
            var rolesFieldName = $"{rolesField["name"]}";
            return rolesFieldName;
        }

        private static JToken GetRolesField(JObject airtableSchema)
        {
            var originalTableName = GetUsersTableName(airtableSchema);
            if (String.IsNullOrEmpty(originalTableName)) return null;
            if (string.IsNullOrEmpty(originalTableName))
                throw new Exception("User table not found in the schema.");

            var tablesArray = airtableSchema["tables"] as JArray;
            if (tablesArray == null)
                throw new Exception("The schema does not contain a 'tables' array.");

            var table = tablesArray
                .FirstOrDefault(fod => $"{fod["name"]}" == originalTableName);

            if (table == null)
                throw new Exception($"Table '{originalTableName}' not found in the schema.");

            var fields = table["fields"] as JArray;
            if (fields == null)
                throw new Exception($"Fields not found in table '{originalTableName}'.");

            var rolesField = fields
                .FirstOrDefault(fod => $"{fod["name"]}".Contains("role", StringComparison.OrdinalIgnoreCase));

            if (rolesField == null)
                throw new Exception("Roles field not found in the user table.");

            return rolesField;
        }

        private static string GetAirtableRoles(string currentDefault, JObject airtableSchema)
        {
            var rolesField = GetRolesField(airtableSchema);
            if (rolesField is null) return null;
            var options = rolesField["options"];
            if (options == null)
                return "Admin,User,Guest";

            var choices = options["choices"] as JArray;
            if (choices == null)
                return "Admin,User,Guest";

            var choiceNames = choices.Select(choice => $"{choice["name"]}");
            var roles = string.Join(",", choiceNames);
            return roles;
        }

        private static string GetAirtableUserRoleName(string currentDefault, JObject airtableSchema)
        {
            var rolesField = GetRolesField(airtableSchema);
            if (rolesField is null) return null;
            var options = rolesField["options"];
            if (options == null)
                return "User";

            var choices = options["choices"] as JArray;
            if (choices == null)
                return "User";

            var choiceNames = choices.Select(choice => $"{choice["name"]}");
            var firstNonAdminOrGuestRole = choiceNames
                .FirstOrDefault(fod =>
                    !fod.Equals("admin", StringComparison.OrdinalIgnoreCase) &&
                    !fod.Equals("guest", StringComparison.OrdinalIgnoreCase));

            return firstNonAdminOrGuestRole ?? "User";
        }

        private static HttpClient httpClient = new HttpClient();

        private static async Task<JObject> GetAirtableSchemaFromBaseId(string baseId)
        {
            // Priority 1: Check effortless.env (project-level)
            var envFile = SsotmeEnvFile.TryLoadFromNearestProject();
            var pat = envFile?.GetValue("AIRTABLE_PAT") ?? envFile?.GetValue("AIRTABLE_API_KEY");

            // Priority 2: Fall back to ~/.ssotme/ssotme.key
            if (string.IsNullOrEmpty(pat))
            {
                var keyValuePair = SSOTMEKey.CurrentKey?.APIKeys.FirstOrDefault(fod => fod.Key == "airtable");
                pat = keyValuePair?.Value;
            }

            if (string.IsNullOrEmpty(pat))
            {
                Console.WriteLine("Airtable API key is missing. Add AIRTABLE_PAT to effortless.env or run: ssotme -setAccountAPIKey=airtable/YOUR_PAT");
                return null;
            }

            // Setup HttpClient to make the Meta API request
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);

            // Airtable Meta API endpoints
            string tablesUrl = $"https://api.airtable.com/v0/meta/bases/{baseId}/tables";
            string basesUrl = "https://api.airtable.com/v0/meta/bases";  // This gets all bases

            try
            {
                // Concurrently fetch tables and bases info
                var tablesTask = httpClient.GetAsync(tablesUrl);
                var basesTask = httpClient.GetAsync(basesUrl);
                await Task.WhenAll(tablesTask, basesTask);

                // Handle tables response
                var tablesResponse = tablesTask.Result;
                if (!tablesResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to fetch Airtable tables: {tablesResponse.StatusCode} {tablesResponse.ReasonPhrase}");
                    return new JObject();
                }
                var tablesContent = await tablesResponse.Content.ReadAsStringAsync();

                // Handle bases response
                var basesResponse = basesTask.Result;
                if (!basesResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to fetch Airtable bases info: {basesResponse.StatusCode} {basesResponse.ReasonPhrase}");
                    return new JObject();
                }
                var basesContent = await basesResponse.Content.ReadAsStringAsync();

                // Parse JSON and find the specific base
                JObject basesData = JObject.Parse(basesContent);
                JObject tablesData = JObject.Parse(tablesContent);
                var baseData = basesData["bases"].Children<JObject>().FirstOrDefault(b => b["id"].ToString() == baseId);

                if (baseData == null)
                {
                    Console.WriteLine("Base not found with the specified ID.");
                    return new JObject();
                }

                // Combine the base name and tables into one JObject
                JObject combined = new JObject
                {
                    ["name"] = baseData["name"],
                    ["tables"] = tablesData["tables"]
                };

                return combined;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred while fetching Airtable schema: {ex.Message}");
                return new JObject();  // Return an empty object in case of exceptions
            }
        }


        private static string FindBaseId(JObject responses)
        {
            var replacements = responses["replacements"];
            if (replacements is null) return null;
            var baseIdJO = replacements.FirstOrDefault(fod => $"{fod["key"]}" == "base-id");
            if (baseIdJO is null) return null;
            var baseId = baseIdJO["value"];
            return $"{baseId}";
        }

        //private static string GetCurrentValue(string key, bool isSecret)
        private static string GetCurrentValue(this JObject container, string key)
        {
            // Try to get the value from the local secrets or response file.
            string value = (string)container["replacements"]?.FirstOrDefault(x => (string)x["key"] == key)?["value"];
            return value;
        }

        //private static string GetCurrentValue(string key, bool isSecret)
        private static bool SkipValue(this JObject container, string key)
        {
            // Try to get the value from the local secrets or response file.
            string value = (string)container["replacements"]?.FirstOrDefault(x => (string)x["key"] == key)?["skip"];
            return String.Equals(value ,"true", StringComparison.OrdinalIgnoreCase);
        }


        private static string GetDefaultValue(this JObject parentContainer, string key, JToken replacement, JObject parentSeedReplacements)
        {
            var result = (string)parentContainer["replacements"]?.FirstOrDefault(x => (string)x["key"] == key)?["value"];
            if (result == null)
            {
                result = (string)parentSeedReplacements["replacements"]?.FirstOrDefault(x => (string)x["key"] == key)?["value"];
            }
            return result ?? $"{replacement["default"]}";
        }


        private static bool IsTextUnusedInDirectory(DirectoryInfo directoryInfo, string findText, string defaultReplacementText)
        {
            if (directoryInfo.IsIgnored()) return true;
            foreach (FileInfo file in directoryInfo.GetFiles())
            {
                if (file.Name == "ssotme-seed.json") continue;
                if (FileContainsText(file.FullName, findText)) return false;
                if (FileContainsText(file.FullName, defaultReplacementText)) return false;
            }

            foreach (DirectoryInfo subDir in directoryInfo.GetDirectories())
            {
                if (!IsTextUnusedInDirectory(subDir, findText, defaultReplacementText)) return false;
            }

            return true;
        }

        private static bool FileContainsText(string filePath, string findText)
        {
            try
            {
                string content = File.ReadAllText(filePath);
                return content.Contains(findText);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while reading the file {filePath}: {ex.Message}");
                return false;
            }
        }

        private static void AddOrUpdateSecret(JObject secrets, string key, string value)
        {
            JToken foundSecret = secrets.SelectToken($"$.replacements[?(@.key == '{key}')]");
            if (foundSecret != null)
            {
                foundSecret["value"] = value;
            }
            else
            {
                secrets["replacements"] = secrets["replacements"] ?? new JArray();
                var replacements = secrets["replacements"].ToObject<JArray>();
                replacements.Add(new JObject { ["key"] = key, ["value"] = value });
                secrets["replacements"] = replacements;
            }
        }

        private static void AddOrUpdateResponse(JObject responses, string key, string value)
        {
            JToken foundResponse = responses.SelectToken($"$.replacements[?(@.key == '{key}')]");
            if (foundResponse != null)
            {
                foundResponse["value"] = value;
                if (String.IsNullOrEmpty(value)) foundResponse["skip"] = true;
            }
            else
            {
                responses["replacements"] = responses["replacements"] ?? new JArray();
                var replacements = responses["replacements"].ToObject<JArray>();
                foundResponse = new JObject { ["key"] = key, ["value"] = value };
                if (String.IsNullOrEmpty(value)) foundResponse["skip"] = true;
                replacements.Add(foundResponse);
                responses["replacements"] = replacements;
            }
        }

        private static void ReplaceInFiles(DirectoryInfo rootPath, string findText, string replaceText)
        {
            foreach (var file in rootPath.GetFiles("*", SearchOption.AllDirectories))
            {
                if (file.FullName.Replace("\\", "/").Contains("/.git/")) continue;
                if (file.Name == "ssotme-seed.json") continue;
                var content = File.ReadAllText(file.FullName);
                var newContent = Regex.Replace(content, findText, replaceText);

                if (newContent != content) File.WriteAllText(file.FullName, newContent);
            }
        }
    }
}