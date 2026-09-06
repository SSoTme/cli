using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Effortless.Cli.E2E.Harness;

internal sealed record MockLog(string Level, string Text);

internal sealed record CapturedToolRequest(
    string ToolName,
    string Version,
    string RawJson,
    JsonElement Json,
    IReadOnlyList<string> CliParams,
    IReadOnlyList<string> CliInput,
    string? CliOutput,
    string? CliJwt,
    string? CliAccount,
    int CliWaitTimeout,
    string? CliTranspiler,
    bool CliDebug,
    string? TranspilerName,
    string? TranspilerLowerHyphenName,
    string InputFileSetXml,
    IReadOnlyList<FileSetEntry> InputFiles);

internal abstract class ToolBehavior
{
    private readonly List<MockLog> _logs = [];

    public IReadOnlyList<MockLog> Logs => _logs;

    public ToolBehavior WithLogs(params MockLog[] logs)
    {
        ArgumentNullException.ThrowIfNull(logs);
        _logs.AddRange(logs);
        return this;
    }

    public static ToolBehavior Echo() => new FilesBehavior(null);

    public static ToolBehavior Files(params FileSetEntry[] files) => new FilesBehavior(files);

    public static ToolBehavior Exception(
        string message,
        string? inner = null,
        string? stackTrace = null) =>
        new ExceptionBehavior(message, inner, stackTrace);

    public static ToolBehavior Status(int statusCode, string body) =>
        new RawBehavior(statusCode, body, "application/json");

    public static ToolBehavior Text(string body) =>
        new RawBehavior(200, body, "text/plain");

    public static ToolBehavior Delay(int milliseconds, ToolBehavior then) =>
        new DelayBehavior(milliseconds, then);

    public static ToolBehavior Async(int pendingPolls, ToolBehavior then) =>
        new AsyncBehavior(pendingPolls, then, notFound: false);

    public static ToolBehavior AsyncNotFound(int pendingPolls = 0) =>
        new AsyncBehavior(pendingPolls, Exception("unused"), notFound: true);

    internal abstract Task<MockResponse> CreateResponse(
        MockToolServer server,
        CapturedToolRequest request,
        string? taskStatus = null);

    internal object[] SerializedLogs() =>
        Logs.Select(log => (object)new
        {
            Level = log.Level,
            Text = log.Text,
            Timestamp = DateTimeOffset.UtcNow,
        }).ToArray();

    private sealed class FilesBehavior(IReadOnlyList<FileSetEntry>? files) : ToolBehavior
    {
        internal override Task<MockResponse> CreateResponse(
            MockToolServer server,
            CapturedToolRequest request,
            string? taskStatus = null)
        {
            var output = files;
            if (output is null)
            {
                var first = request.InputFiles.FirstOrDefault()
                    ?? throw new InvalidOperationException("Echo behavior requires at least one input file.");
                output =
                [
                    FileSetEntry.TextFile(
                        "Output.txt",
                        first.Text.ToUpperInvariant(),
                        alwaysOverwrite: true),
                ];
            }

            return Task.FromResult(
                MockResponse.Json(
                    server.BuildPayload(
                        request.ToolName,
                        output,
                        SerializedLogs(),
                        taskStatus: taskStatus)));
        }
    }

    private sealed class ExceptionBehavior(
        string message,
        string? inner,
        string? stackTrace) : ToolBehavior
    {
        internal override Task<MockResponse> CreateResponse(
            MockToolServer server,
            CapturedToolRequest request,
            string? taskStatus = null) =>
            Task.FromResult(
                MockResponse.Json(
                    server.BuildPayload(
                        request.ToolName,
                        [],
                        SerializedLogs(),
                        exception: new
                        {
                            Message = message,
                            StackTrace = stackTrace,
                            InnerException = inner is null ? null : new { Message = inner },
                        },
                        taskStatus: taskStatus)));
    }

    private sealed class RawBehavior(int statusCode, string body, string contentType) : ToolBehavior
    {
        internal override Task<MockResponse> CreateResponse(
            MockToolServer server,
            CapturedToolRequest request,
            string? taskStatus = null) =>
            Task.FromResult(new MockResponse(statusCode, contentType, body));
    }

    private sealed class DelayBehavior(int milliseconds, ToolBehavior then) : ToolBehavior
    {
        internal override async Task<MockResponse> CreateResponse(
            MockToolServer server,
            CapturedToolRequest request,
            string? taskStatus = null)
        {
            if (milliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(milliseconds));
            }

            await Task.Delay(milliseconds, server.StoppingToken);
            return await then.CreateResponse(server, request, taskStatus);
        }
    }

    private sealed class AsyncBehavior(
        int pendingPolls,
        ToolBehavior then,
        bool notFound) : ToolBehavior
    {
        internal override Task<MockResponse> CreateResponse(
            MockToolServer server,
            CapturedToolRequest request,
            string? taskStatus = null)
        {
            if (pendingPolls < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pendingPolls));
            }

            var taskId = Guid.NewGuid().ToString("N");
            server.RegisterAsyncTask(taskId, request, pendingPolls, then, notFound);
            return Task.FromResult(
                MockResponse.Json(
                    server.BuildPayload(
                        request.ToolName,
                        [],
                        SerializedLogs(),
                        taskId: taskId,
                        taskStatus: "pending")));
        }
    }
}

internal sealed record MockResponse(int StatusCode, string ContentType, string Body)
{
    public static MockResponse Json(object value) =>
        new(
            200,
            "application/json",
            JsonSerializer.Serialize(value, MockToolServer.JsonOptions));
}

internal sealed class MockToolServer : IAsyncDisposable
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false,
    };

    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ToolBehavior>> _behaviors =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, AsyncTaskState> _asyncTasks = new();
    private readonly ConcurrentQueue<Exception> _failures = new();
    private readonly SemaphoreSlim _requestArrived = new(0);
    private readonly Task _listenTask;
    private bool _disposed;

    public MockToolServer()
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

    public ConcurrentDictionary<string, string> AuthResponses { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public ConcurrentQueue<CapturedToolRequest> Requests { get; } = new();

    internal CancellationToken StoppingToken => _stopping.Token;

    public Uri ToolUri(string name, string version = "v2026.01.01.0001") =>
        new(BaseUri, $"tools/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(version)}/");

    public void Enqueue(string toolName, params ToolBehavior[] behaviors)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            throw new ArgumentException("Tool name cannot be empty.", nameof(toolName));
        }

        if (behaviors.Length == 0)
        {
            throw new ArgumentException("At least one behavior is required.", nameof(behaviors));
        }

        var queue = _behaviors.GetOrAdd(toolName, _ => new ConcurrentQueue<ToolBehavior>());
        foreach (var behavior in behaviors)
        {
            queue.Enqueue(behavior ?? throw new ArgumentNullException(nameof(behaviors)));
        }
    }

    public async Task<CapturedToolRequest> WaitForRequestAsync(
        int ordinal = 1,
        int timeoutMs = 10_000)
    {
        if (ordinal < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        using var timeout = new CancellationTokenSource(timeoutMs);
        while (Requests.Count < ordinal)
        {
            await _requestArrived.WaitAsync(timeout.Token);
            ThrowIfFaulted();
        }

        ThrowIfFaulted();
        return Requests.ElementAt(ordinal - 1);
    }

    public void ThrowIfFaulted()
    {
        if (_failures.TryPeek(out var failure))
        {
            throw new InvalidOperationException("The mock tool server failed.", failure);
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

        _requestArrived.Dispose();
        _stopping.Dispose();
        ThrowIfFaulted();
    }

    internal object BuildPayload(
        string toolName,
        IReadOnlyList<FileSetEntry> files,
        object[] logs,
        object? exception = null,
        string? taskId = null,
        string? taskStatus = null)
    {
        var xml = FileSetXml.Build(files);
        return new
        {
            TranspileRequest = new
            {
                ZippedOutputFileSet = FileSetXml.GzipToBase64(xml),
            },
            Transpiler = new
            {
                Name = toolName,
                LowerHyphenName = toolName,
            },
            Logs = logs,
            SSoTmeProject = (object?)null,
            Exception = exception,
            TaskId = taskId,
            TaskStatus = taskStatus,
        };
    }

    internal void RegisterAsyncTask(
        string taskId,
        CapturedToolRequest request,
        int pendingPolls,
        ToolBehavior terminalBehavior,
        bool notFound) =>
        _asyncTasks[taskId] =
            new AsyncTaskState(request, pendingPolls, terminalBehavior, notFound);

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
                _requestArrived.Release();
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
                new MockResponse(
                    500,
                    "application/json",
                    JsonSerializer.Serialize(new { error = exception.Message })));
            _requestArrived.Release();
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var method = context.Request.HttpMethod;
        var segments = context.Request.Url?.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            ?? [];

        if (method == "POST" && segments is ["bridge"])
        {
            await HandleBridgeAsync(context);
            return;
        }

        if (segments.Length >= 3
            && segments[0].Equals("tools", StringComparison.OrdinalIgnoreCase))
        {
            var toolName = Uri.UnescapeDataString(segments[1]);
            var version = Uri.UnescapeDataString(segments[2]);
            if (method == "POST" && segments.Length == 3)
            {
                await HandleToolPostAsync(context, toolName, version);
                return;
            }

            if (method == "GET"
                && segments.Length == 5
                && segments[3].Equals("task", StringComparison.OrdinalIgnoreCase))
            {
                await HandleTaskPollAsync(context, segments[4]);
                return;
            }

            if (method == "GET" && segments.Length == 3)
            {
                await WriteResponseAsync(
                    context.Response,
                    MockResponse.Json(new { status = "healthy" }));
                return;
            }
        }

        await WriteResponseAsync(
            context.Response,
            new MockResponse(404, "application/json", """{"error":"route not found"}"""));
    }

    private async Task HandleBridgeAsync(HttpListenerContext context)
    {
        var raw = await ReadBodyAsync(context.Request);
        using var document = ParseJson(raw);
        var root = document.RootElement;
        var mode = ReadStringArray(root, "cliParams")
            .Select(ParseParameter)
            .FirstOrDefault(pair => pair.Key.Equals("mode", StringComparison.OrdinalIgnoreCase))
            .Value ?? "list";

        string fileName;
        string contents;
        if (mode.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            // The live bridge emits this legacy file name; the CLI renames it.
            fileName = "ssotme-tools.json";
            contents = IndexJson;
        }
        else if (AuthResponses.TryGetValue(mode, out var authResponse))
        {
            fileName = "auth-result.json";
            contents = authResponse;
        }
        else
        {
            throw new InvalidOperationException($"No bridge response is configured for mode '{mode}'.");
        }

        var payload = BuildPayload(
            "cli-cloud-bridge",
            [FileSetEntry.TextFile(fileName, contents, alwaysOverwrite: true)],
            []);
        await WriteResponseAsync(context.Response, MockResponse.Json(payload));
    }

    private async Task HandleToolPostAsync(
        HttpListenerContext context,
        string toolName,
        string version)
    {
        var raw = await ReadBodyAsync(context.Request);
        using var document = ParseJson(raw);
        var request = CaptureRequest(toolName, version, raw, document.RootElement);
        Requests.Enqueue(request);
        _requestArrived.Release();

        if (!_behaviors.TryGetValue(toolName, out var queue) || !queue.TryDequeue(out var behavior))
        {
            throw new InvalidOperationException(
                $"No scripted response remains for tool '{toolName}'. Tests must configure every response explicitly.");
        }

        var response = await behavior.CreateResponse(this, request);
        await WriteResponseAsync(context.Response, response);
    }

    private async Task HandleTaskPollAsync(HttpListenerContext context, string taskId)
    {
        if (!_asyncTasks.TryGetValue(taskId, out var state) || state.NotFound)
        {
            await WriteResponseAsync(
                context.Response,
                new MockResponse(404, "application/json", """{"error":"task not found"}"""));
            return;
        }

        if (state.PendingPolls > 0)
        {
            state.PendingPolls--;
            await WriteResponseAsync(
                context.Response,
                MockResponse.Json(
                    BuildPayload(
                        state.Request.ToolName,
                        [],
                        [],
                        taskId: taskId,
                        taskStatus: "pending")));
            return;
        }

        _asyncTasks.TryRemove(taskId, out _);
        var response = await state.TerminalBehavior.CreateResponse(
            this,
            state.Request,
            taskStatus: "completed");
        await WriteResponseAsync(context.Response, response);
    }

    private static CapturedToolRequest CaptureRequest(
        string toolName,
        string version,
        string raw,
        JsonElement root)
    {
        var zippedInput = Property(root, "transpileRequest")
            is { ValueKind: JsonValueKind.Object } request
            ? Property(request, "zippedInputFileSet")?.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(zippedInput))
        {
            throw new InvalidDataException(
                "Request is missing transpileRequest.zippedInputFileSet.");
        }

        var xml = FileSetXml.GunzipBase64ToString(zippedInput);
        var duplicateXml = Property(root, "cliInputFileSetXml")?.GetString();
        if (!string.Equals(xml, duplicateXml, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "cliInputFileSetXml does not match the decompressed zippedInputFileSet.");
        }

        var transpiler = Property(root, "transpiler");
        return new CapturedToolRequest(
            toolName,
            version,
            raw,
            root.Clone(),
            ReadStringArray(root, "cliParams"),
            ReadStringArray(root, "cliInput"),
            Property(root, "cliOutput")?.GetString(),
            Property(root, "cliJwt")?.GetString(),
            Property(root, "cliAccount")?.GetString(),
            Property(root, "cliWaitTimeout")?.GetInt32() ?? 0,
            Property(root, "cliTranspiler")?.GetString(),
            Property(root, "cliDebug")?.GetBoolean() ?? false,
            transpiler is { ValueKind: JsonValueKind.Object }
                ? Property(transpiler.Value, "name")?.GetString()
                : null,
            transpiler is { ValueKind: JsonValueKind.Object }
                ? Property(transpiler.Value, "lowerHyphenName")?.GetString()
                : null,
            xml,
            FileSetXml.Parse(xml));
    }

    private static async Task<string> ReadBodyAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(
            request.InputStream,
            request.ContentEncoding ?? Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static JsonDocument ParseJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidDataException("Request body is empty.");
        }

        try
        {
            return JsonDocument.Parse(raw);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Request body is not valid JSON.", exception);
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

        if (value.Value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Request property '{name}' is not an array.");
        }

        return value.Value.EnumerateArray()
            .Select(item =>
                item.GetString()
                ?? throw new InvalidDataException($"Request property '{name}' contains null."))
            .ToArray();
    }

    private static KeyValuePair<string, string?> ParseParameter(string value)
    {
        var separator = value.IndexOf('=');
        return separator < 0
            ? new KeyValuePair<string, string?>(value, null)
            : new KeyValuePair<string, string?>(
                value[..separator],
                value[(separator + 1)..]);
    }

    private static async Task WriteResponseAsync(
        HttpListenerResponse response,
        MockResponse mockResponse)
    {
        var bytes = Encoding.UTF8.GetBytes(mockResponse.Body);
        response.StatusCode = mockResponse.StatusCode;
        response.ContentType = mockResponse.ContentType;
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

    private sealed class AsyncTaskState(
        CapturedToolRequest request,
        int pendingPolls,
        ToolBehavior terminalBehavior,
        bool notFound)
    {
        public CapturedToolRequest Request { get; } = request;

        public int PendingPolls { get; set; } = pendingPolls;

        public ToolBehavior TerminalBehavior { get; } = terminalBehavior;

        public bool NotFound { get; } = notFound;
    }
}
