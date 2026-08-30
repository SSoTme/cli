using System.Text.Json;
using System.Text.Json.Nodes;

namespace Effortless.Cli.E2E.Harness;

internal sealed class IndexFixture
{
    private IndexFixture(string json, Uri bridgeUri, IReadOnlyDictionary<string, string> toolUrls)
    {
        Json = json;
        BridgeUri = bridgeUri;
        ToolUrls = toolUrls;
    }

    public string Json { get; }

    public Uri BridgeUri { get; }

    public IReadOnlyDictionary<string, string> ToolUrls { get; }

    public static IndexFixture Load(MockToolServer server, string fixtureName = "base")
    {
        ArgumentNullException.ThrowIfNull(server);
        if (string.IsNullOrWhiteSpace(fixtureName)
            || fixtureName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The index fixture name is invalid.", nameof(fixtureName));
        }

        var path = Path.Combine(
            CliUnderTest.Root,
            "tests",
            "fixtures",
            "index",
            fixtureName + ".json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Index fixture '{fixtureName}' does not exist.", path);
        }

        var rendered = File.ReadAllText(path)
            .Replace("{port}", server.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        JsonNode root;
        try
        {
            root = JsonNode.Parse(rendered)
                ?? throw new InvalidDataException($"Index fixture '{fixtureName}' is JSON null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Index fixture '{fixtureName}' is malformed.", exception);
        }

        var normalized = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        var urls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cli-cloud-bridge"] = server.BridgeUri.ToString(),
        };
        foreach (var tool in root["transpilerVersions"]?.AsObject()
            ?? throw new InvalidDataException($"Index fixture '{fixtureName}' has no transpilerVersions object."))
        {
            var versions = tool.Value?.AsObject()
                ?? throw new InvalidDataException($"Index tool '{tool.Key}' has no versions object.");
            var head = versions
                .Where(version =>
                    version.Value?["metaData"]?["isHeadVersion"]?.GetValue<bool>() == true)
                .Select(version => version.Value)
                .SingleOrDefault();
            var post = head?["urls"]?["post"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(post))
            {
                var shortName = tool.Key.Split('/').Last();
                urls[shortName] = post;
                urls[tool.Key] = post;
            }
        }

        return new IndexFixture(normalized, server.BridgeUri, urls);
    }
}
