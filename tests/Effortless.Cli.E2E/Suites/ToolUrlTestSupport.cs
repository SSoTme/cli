using System.Text.Json;
using System.Text.Json.Nodes;
using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

internal static class ToolUrlTestSupport
{
    public static string ToolUrlsPath(Sandbox sandbox) =>
        Path.Combine(sandbox.HomePath, ".effortless", "tool_urls.json");

    public static void WriteToolUrls(
        Sandbox sandbox,
        IReadOnlyDictionary<string, string> urls)
    {
        sandbox.WriteHomeFile(
            ".effortless/tool_urls.json",
            JsonSerializer.Serialize(urls, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static JsonObject ReadToolUrls(Sandbox sandbox)
    {
        var path = ToolUrlsPath(sandbox);
        return JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new InvalidDataException("tool_urls.json is not a JSON object.");
    }

    public static void WriteMinimalProject(Sandbox sandbox)
    {
        sandbox.WriteFile(
            "effortless.json",
            """
            {
              "Name": "tool-url-project",
              "ProjectSettings": [],
              "ProjectTranspilers": []
            }
            """);
    }
}
