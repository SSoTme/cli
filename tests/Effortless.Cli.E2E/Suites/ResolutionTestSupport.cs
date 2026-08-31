using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

internal sealed record ResolutionProjectStep(
    string Name,
    string RelativePath,
    string CommandLine,
    string? PinnedVersion = null,
    string? LastVersionUsed = null,
    bool IsDisabled = false);

internal static class ResolutionTestSupport
{
    public const string HeadVersion = "v2026.01.01.0001";
    public const string OldVersion = "v2025.12.31.2359";
    public const string BootstrapBridgeUrl =
        "https://ssotme-cli-cloud-bridge-v2026-04-24-1853-cmvbd4phczmeg.7pktzg2z971j0.cpln.app";

    public static void SeedHome(
        Sandbox sandbox,
        IndexFixture index,
        Uri? bridgeUri = null)
    {
        sandbox.SeedHome(index);
        WriteToolUrls(
            sandbox,
            new Dictionary<string, string>
            {
                ["cli-cloud-bridge"] = (bridgeUri ?? index.BridgeUri).ToString(),
            });
    }

    public static void SeedProject(
        Sandbox sandbox,
        params ResolutionProjectStep[] steps)
    {
        var project = new
        {
            Name = "resolution-project",
            SSoTmeProjectId =
                "4ec92dc6-8de5-43ed-9c30-f55a66407d07",
            ProjectSettings = new[]
            {
                new { Name = "project-name", Value = "resolution-project" },
            },
            ProjectTranspilers = steps.Select(
                step => new
                {
                    step.Name,
                    step.RelativePath,
                    step.CommandLine,
                    step.PinnedVersion,
                    step.LastVersionUsed,
                    step.IsDisabled,
                }),
        };
        sandbox.WriteFile(
            "effortless.json",
            JsonSerializer.Serialize(
                project,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition =
                        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                }));
        sandbox.WriteFile("in.txt", "resolution input");
    }

    public static void WriteToolUrls(
        Sandbox sandbox,
        IReadOnlyDictionary<string, string> urls)
    {
        sandbox.WriteHomeFile(
            ".ssotme/tool_urls.json",
            JsonSerializer.Serialize(urls, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static JsonObject ReadHomeObject(Sandbox sandbox, string relativePath)
    {
        var path = Path.Combine(sandbox.HomePath, relativePath);
        return JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new InvalidDataException($"'{relativePath}' is not a JSON object.");
    }

    public static JsonObject ReadProject(Sandbox sandbox) =>
        sandbox.ProjectFile.AsObject();

    public static JsonObject FindStep(JsonObject project, string commandLinePrefix)
    {
        var steps = project["ProjectTranspilers"]?.AsArray()
            ?? throw new InvalidDataException("ProjectTranspilers is missing.");
        return steps
            .Select(node => node?.AsObject())
            .Single(
                step => step?["CommandLine"]?.GetValue<string>()
                    .StartsWith(commandLinePrefix, StringComparison.OrdinalIgnoreCase) == true)
            ?? throw new InvalidDataException(
                $"No project step begins with '{commandLinePrefix}'.");
    }
}

internal sealed record CapturedBridgeRequest(
    string RawJson,
    JsonElement Json,
    IReadOnlyList<string> CliParams,
    string? CliTranspiler,
    string? TranspilerName);

internal sealed class ResolutionBridgeServer : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentQueue<Exception> _failures = new();
    private readonly Task _listenTask;
    private bool _disposed;

    public ResolutionBridgeServer()
    {
        Port = GetFreePort();
        BaseUri = new Uri($"http://127.0.0.1:{Port}/");
        BridgeUri = new Uri(BaseUri, "bridge/");
        _listener.Prefixes.Add(BaseUri.ToString());
        _listener.Start();
        _listenTask = ListenAsync();
    }

    public int Port { get; }

    public Uri BaseUri { get; }

    public Uri BridgeUri { get; }

    public string IndexJson { get; set; } = """{"transpilerVersions":{}}""";

    public int StatusCode { get; set; } = 200;

    public ConcurrentQueue<CapturedBridgeRequest> Requests { get; } = new();

    public void ThrowIfFaulted()
    {
        if (_failures.TryPeek(out var failure))
        {
            throw new InvalidOperationException("The resolution bridge server failed.", failure);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopping.Cancel();
        _listener.Stop();
        _listener.Close();
        try
        {
            await _listenTask;
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }

        _stopping.Dispose();
        ThrowIfFaulted();
    }

    private async Task ListenAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception exception) when (
                _stopping.IsCancellationRequested
                && exception is HttpListenerException or ObjectDisposedException)
            {
                break;
            }
            catch (Exception exception)
            {
                _failures.Enqueue(exception);
                break;
            }

            _ = HandleSafelyAsync(context);
        }
    }

    private async Task HandleSafelyAsync(HttpListenerContext context)
    {
        try
        {
            if (context.Request.HttpMethod != "POST"
                || !string.Equals(
                    context.Request.Url?.AbsolutePath.TrimEnd('/'),
                    "/bridge",
                    StringComparison.OrdinalIgnoreCase))
            {
                await WriteResponseAsync(
                    context.Response,
                    404,
                    """{"error":"route not found"}""");
                return;
            }

            using var reader = new StreamReader(
                context.Request.InputStream,
                context.Request.ContentEncoding ?? Encoding.UTF8);
            var raw = await reader.ReadToEndAsync();
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            var transpiler = Property(root, "transpiler");
            Requests.Enqueue(
                new CapturedBridgeRequest(
                    raw,
                    root.Clone(),
                    ReadStringArray(root, "cliParams"),
                    Property(root, "cliTranspiler")?.GetString(),
                    transpiler is { ValueKind: JsonValueKind.Object }
                        ? Property(transpiler.Value, "name")?.GetString()
                        : null));

            if (StatusCode != 200)
            {
                await WriteResponseAsync(
                    context.Response,
                    StatusCode,
                    """{"error":"configured bridge failure"}""");
                return;
            }

            var xml = FileSetXml.Build(
                [FileSetEntry.TextFile("ssotme-tools.json", IndexJson, alwaysOverwrite: true)]);
            var payload = new
            {
                TranspileRequest = new
                {
                    ZippedOutputFileSet = FileSetXml.GzipToBase64(xml),
                },
                Transpiler = new
                {
                    Name = "cli-cloud-bridge",
                    LowerHyphenName = "cli-cloud-bridge",
                },
                Logs = Array.Empty<object>(),
                SSoTmeProject = (object?)null,
                Exception = (object?)null,
                TaskId = (string?)null,
                TaskStatus = (string?)null,
            };
            await WriteResponseAsync(
                context.Response,
                200,
                JsonSerializer.Serialize(payload));
        }
        catch (Exception exception)
        {
            _failures.Enqueue(exception);
            try
            {
                await WriteResponseAsync(
                    context.Response,
                    500,
                    JsonSerializer.Serialize(new { error = exception.Message }));
            }
            catch
            {
                // The client may have already disconnected.
            }
        }
    }

    private static JsonElement? Property(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string name)
    {
        var value = Property(root, name);
        if (value is null || value.Value.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        return value.Value.EnumerateArray()
            .Select(item => item.GetString() ?? throw new InvalidDataException("Null CLI parameter."))
            .ToArray();
    }

    private static async Task WriteResponseAsync(
        HttpListenerResponse response,
        int statusCode,
        string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        response.StatusCode = statusCode;
        response.ContentType = "application/json";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = bytes.Length;
        try
        {
            await response.OutputStream.WriteAsync(bytes);
        }
        finally
        {
            response.Close();
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
