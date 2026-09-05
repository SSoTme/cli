using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Effortless.Cli.E2E.Harness;

internal sealed record CapturedAuthRequest(
    string Mode,
    string RawJson,
    IReadOnlyDictionary<string, string> Parameters);

internal sealed class AuthTestBridge : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentQueue<Exception> _failures = new();
    private readonly Task _listenTask;
    private bool _disposed;

    public AuthTestBridge()
    {
        Port = GetFreePort();
        BridgeUri = new Uri($"http://127.0.0.1:{Port}/bridge/");
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _listenTask = ListenAsync();
    }

    public int Port { get; }

    public Uri BridgeUri { get; }

    public ConcurrentDictionary<string, string> Responses { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public ConcurrentQueue<CapturedAuthRequest> Requests { get; } = new();

    public void SeedHome(Sandbox sandbox)
    {
        sandbox.WriteHomeFile(
            ".effortless/tool_urls.json",
            JsonSerializer.Serialize(
                new Dictionary<string, string>
                {
                    ["cli-cloud-bridge"] = BridgeUri.ToString(),
                },
                new JsonSerializerOptions { WriteIndented = true }));
    }

    public void ThrowIfFaulted()
    {
        if (_failures.TryPeek(out var failure))
        {
            throw new InvalidOperationException("The auth test bridge failed.", failure);
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
            await HandleAsync(context);
        }
        catch (Exception exception)
        {
            _failures.Enqueue(exception);
            await WriteResponseAsync(
                context.Response,
                500,
                JsonSerializer.Serialize(new { error = exception.Message }));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        if (!context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase)
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

        var rawJson = await ReadBodyAsync(context.Request);
        using var document = JsonDocument.Parse(rawJson);
        var cliParams = ReadStringArray(document.RootElement, "cliParams");
        var parameters = cliParams
            .Select(ParseParameter)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var mode = parameters.GetValueOrDefault("mode") ?? "list";
        Requests.Enqueue(new CapturedAuthRequest(mode, rawJson, parameters));

        if (!Responses.TryGetValue(mode, out var responseJson))
        {
            throw new InvalidOperationException(
                $"No auth response is configured for mode '{mode}'.");
        }

        var fileSetXml = FileSetXml.Build(
            [FileSetEntry.TextFile("auth-result.json", responseJson, alwaysOverwrite: true)]);
        var payload = new
        {
            TranspileRequest = new
            {
                ZippedOutputFileSet = FileSetXml.GzipToBase64(fileSetXml),
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
            JsonSerializer.Serialize(payload, MockToolServer.JsonOptions));
    }

    private static async Task<string> ReadBodyAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(
            request.InputStream,
            request.ContentEncoding ?? Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string name)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"Request property '{name}' is not an array.");
            }

            return property.Value.EnumerateArray()
                .Select(item =>
                    item.GetString()
                    ?? throw new InvalidDataException(
                        $"Request property '{name}' contains null."))
                .ToArray();
        }

        return [];
    }

    private static KeyValuePair<string, string> ParseParameter(string parameter)
    {
        var separator = parameter.IndexOf('=');
        return separator < 0
            ? new KeyValuePair<string, string>(parameter, string.Empty)
            : new KeyValuePair<string, string>(
                parameter[..separator],
                parameter[(separator + 1)..]);
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
