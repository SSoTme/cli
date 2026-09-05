using Newtonsoft.Json;

namespace Effortless.Cli.Config;

public static class ToolUrls
{
    public static FileInfo GetToolUrlsFilePath()
    {
        var toolUrlsPath = Path.Combine(
            UserConfigDir.EffortlessDir.FullName,
            "tool_urls.json");

        return new FileInfo(toolUrlsPath);
    }

    public static string TryGetUrlFromFileUrls(string transpilerName)
    {
        try
        {
            var toolUrlsPath = GetToolUrlsFilePath().FullName;
            if (!File.Exists(toolUrlsPath))
            {
                return null;
            }

            var jsonContent = File.ReadAllText(toolUrlsPath);
            var urlMappings =
                JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    jsonContent);

            if (urlMappings != null &&
                urlMappings.ContainsKey(transpilerName))
            {
                return urlMappings[transpilerName];
            }
        }
        catch (Exception ex)
        {
            throw new InvalidDataException(
                $"Error reading ~/.effortless/tool_urls.json: {ex.Message}",
                ex);
        }

        return null;
    }

    public static void SetToolUrl(string toolName, string url)
    {
        try
        {
            var toolUrlsPath = GetToolUrlsFilePath().FullName;

            Dictionary<string, string> urlMappings;
            if (File.Exists(toolUrlsPath))
            {
                var jsonContent = File.ReadAllText(toolUrlsPath);
                urlMappings =
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(
                        jsonContent) ??
                    new Dictionary<string, string>();
            }
            else
            {
                urlMappings = new Dictionary<string, string>();
            }

            urlMappings[toolName] = url;

            var updatedJson = JsonConvert.SerializeObject(
                urlMappings,
                Formatting.Indented);
            File.WriteAllText(toolUrlsPath, updatedJson);
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Error saving tool URL: {ex.Message}",
                ex);
        }
    }

    public static void RemoveToolUrl(string toolName)
    {
        try
        {
            var toolUrlsPath = GetToolUrlsFilePath().FullName;

            if (!File.Exists(toolUrlsPath))
            {
                throw new Exception(
                    "No tool URLs file found. There are no configured tool URLs to remove.");
            }

            var jsonContent = File.ReadAllText(toolUrlsPath);
            var urlMappings =
                JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    jsonContent);

            if (urlMappings == null ||
                !urlMappings.ContainsKey(toolName))
            {
                throw new Exception(
                    $"Tool '{toolName}' is not configured in your tool URLs.");
            }

            urlMappings.Remove(toolName);

            var updatedJson = JsonConvert.SerializeObject(
                urlMappings,
                Formatting.Indented);
            File.WriteAllText(toolUrlsPath, updatedJson);
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Error removing tool URL: {ex.Message}",
                ex);
        }
    }
}
