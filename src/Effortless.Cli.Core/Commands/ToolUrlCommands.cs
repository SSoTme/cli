using Effortless.Cli.Config;

namespace Effortless.Cli.Commands;

public sealed class ToolUrlCommands
{
    public int View(string toolName)
    {
        if (string.IsNullOrEmpty(toolName))
        {
            Console.WriteLine(
                "Error: Tool name is required. Usage: effortless viewUrl <toolname>");
            Console.WriteLine(
                "To list all configured tools, use: effortless listUrls");
            return -1;
        }

        var url = ToolUrls.TryGetUrlFromFileUrls(toolName);
        Console.WriteLine(
            string.IsNullOrEmpty(url)
                ? $"Tool '{toolName}' is not configured in ~/.effortless/tool_urls.json file."
                : $"Tool '{toolName}' is configured with URL: {url}");
        return 0;
    }

    public int Set(string setting)
    {
        if (string.IsNullOrEmpty(setting))
        {
            Console.WriteLine(
                "Error: Tool name and URL are required. Usage: effortless -setUrl toolname=url");
            return 0;
        }

        var parts = setting.Split('=');
        if (parts.Length != 2)
        {
            Console.WriteLine(
                "Error: Invalid format. Usage: effortless -setUrl toolname=url");
            return 0;
        }

        var toolName = parts[0].Trim();
        var url = parts[1].Trim();
        if (string.IsNullOrEmpty(toolName) || string.IsNullOrEmpty(url))
        {
            Console.WriteLine(
                "Error: Invalid format. Usage: effortless -setUrl toolname=url");
            return 0;
        }

        if (toolName.Equals(
                RemoteToolsIndex.BridgeToolName,
                StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                "Warning: 'cli-cloud-bridge' is an internal Effortless-managed tool. Overriding it may break remote tool resolution.");
            Console.WriteLine(
                "To restore default functionality, run: effortless -removeUrl cli-cloud-bridge");
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        ToolUrls.SetToolUrl(toolName, url);
        Console.WriteLine($"Tool '{toolName}' URL set to: {url}");
        return 0;
    }

    public int List(bool debug)
    {
        var path = ToolUrls.GetToolUrlsFilePath();
        if (debug)
        {
            Console.WriteLine($"DEBUG: tool_urls.json path: {path.FullName}");
        }

        if (!path.Exists)
        {
            Console.WriteLine(
                "No tool URLs configured. The ~/.effortless/tool_urls.json file does not exist.");
            Console.WriteLine(
                "Use 'effortless setUrl toolname=url' to configure tool URLs.");
            return 0;
        }

        var mappings =
            Newtonsoft.Json.JsonConvert.DeserializeObject<
                Dictionary<string, string>>(
                File.ReadAllText(path.FullName));
        if (mappings is null || mappings.Count == 0)
        {
            Console.WriteLine(
                "No tool URLs configured in ~/.effortless/tool_urls.json.");
            Console.WriteLine(
                "Use 'effortless setUrl toolname=url' to configure tool URLs.");
            return 0;
        }

        Console.WriteLine("Configured tool URL Overrides:");
        Console.WriteLine();
        foreach (var mapping in mappings.OrderBy(pair => pair.Key))
        {
            Console.WriteLine($"  {mapping.Key}: {mapping.Value}");
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {mappings.Count} tool(s) configured");
        return 0;
    }

    public int Remove(string toolName)
    {
        try
        {
            if (toolName.Equals(
                    RemoteToolsIndex.BridgeToolName,
                    StringComparison.OrdinalIgnoreCase))
            {
                ToolUrls.SetToolUrl(
                    toolName,
                    RemoteToolsIndex.BootstrapBridgeUrl);
                Console.WriteLine(
                    $"Tool '{toolName}' URL restored to built-in default: {RemoteToolsIndex.BootstrapBridgeUrl}");
                return 0;
            }

            ToolUrls.RemoveToolUrl(toolName);
            Console.WriteLine(
                $"Tool '{toolName}' URL has been removed from your user configuration.");
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"Error removing tool URL: {exception.Message}");
        }

        return 0;
    }
}
