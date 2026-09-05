#nullable enable
using System.Net;
using System.Net.Sockets;
using System.Text;
using Effortless.Cli.FileSets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli.LocalTools;

/// <summary>
/// The in-process HTTP host for a project's local tools (step 12 / D30).
/// It speaks the exact contract cloud tools speak: <c>POST /&lt;name&gt;</c>
/// takes a TranspilePayload and answers with Transpiler +
/// TranspileRequest.ZippedOutputFileSet + Logs (+ Exception). <c>GET /</c>
/// lists the tools. Built on HttpListener so the CLI keeps its
/// two-dependency footprint.
/// </summary>
public sealed class LocalToolHost : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ProxiedToolProcess> _processes =
        new(StringComparer.Ordinal);
    private readonly Action<string>? _debug;
    private readonly Action<string>? _requestLog;
    private readonly CancellationTokenSource _stopping = new();
    private HttpListener? _listener;
    private Task? _listenTask;
    private LocalToolCatalog _catalog;
    private string? _fileSetHandlerPath;
    private bool _disposed;

    public LocalToolHost(
        LocalToolCatalog catalog,
        Action<string>? debug = null,
        Action<string>? requestLog = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _debug = debug;
        _requestLog = requestLog;
    }

    public int Port { get; private set; }

    public Uri BaseUri => new($"http://127.0.0.1:{Port}/");

    public LocalToolCatalog Catalog
    {
        get
        {
            lock (_gate)
            {
                return _catalog;
            }
        }
    }

    public string WorkRoot =>
        Path.Combine(_catalog.ProjectRoot, Project.EffortlessProject.LedgerDirectoryName, "local-tools");

    public Uri ToolUri(string name) => new(BaseUri, name);

    /// <summary>Starts listening; 0 binds an ephemeral loopback port.</summary>
    public void Start(int port = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_listener is not null)
        {
            return;
        }

        Directory.CreateDirectory(WorkRoot);
        _fileSetHandlerPath = FileSetHandlerAsset.Extract(WorkRoot);
        Port = port > 0 ? port : FreePort();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _listenTask = ListenAsync();
    }

    /// <summary>
    /// Replaces the catalog (resident mode reload). Processes for tools that
    /// disappeared are stopped; the rest restart lazily on their next request.
    /// </summary>
    public void Reload(LocalToolCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        lock (_gate)
        {
            _catalog = catalog;
            foreach (var (name, process) in _processes.ToList())
            {
                process.Dispose();
                _processes.Remove(name);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopping.Cancel();
        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch
        {
            // Best effort.
        }

        try
        {
            _listenTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Listener shutdown races are expected.
        }

        lock (_gate)
        {
            foreach (var process in _processes.Values)
            {
                process.Dispose();
            }

            _processes.Clear();
        }

        _stopping.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener!.GetContextAsync();
            }
            catch (Exception) when (_stopping.IsCancellationRequested)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleSafelyAsync(context));
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
            try
            {
                await WriteJsonAsync(
                    context.Response,
                    500,
                    JsonConvert.SerializeObject(new { error = exception.Message }));
            }
            catch
            {
                // The client went away.
            }
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var segments = (request.Url?.AbsolutePath ?? "/")
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            await WriteJsonAsync(context.Response, 200, DescribeTools());
            return;
        }

        var name = Uri.UnescapeDataString(segments[0]);
        var tool = Catalog.TryGet(name);
        if (tool is null)
        {
            await WriteJsonAsync(
                context.Response,
                404,
                JsonConvert.SerializeObject(new { error = $"No local tool named '{name}'." }));
            return;
        }

        var remainder = segments.Length > 1
            ? "/" + string.Join('/', segments.Skip(1)) + (request.Url?.Query ?? string.Empty)
            : "/";

        if (request.HttpMethod == "GET" && segments.Length == 1)
        {
            await WriteJsonAsync(
                context.Response,
                200,
                JsonConvert.SerializeObject(new { status = "healthy", tool = tool.Name, runtime = tool.RuntimeName }));
            return;
        }

        string? body = null;
        if (request.HasEntityBody)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
            body = await reader.ReadToEndAsync();
        }

        _requestLog?.Invoke($"[cli] {request.HttpMethod} /{tool.Name}{(remainder == "/" ? string.Empty : remainder)} ({tool.RuntimeName})");

        if (tool.Runtime != LocalToolRuntime.Script)
        {
            var forwarded = await GetProcess(tool)
                .ForwardAsync(request.HttpMethod, remainder, body, _stopping.Token);
            if (forwarded.RawResponseJson is not null)
            {
                await WriteJsonAsync(context.Response, forwarded.StatusCode ?? 200, forwarded.RawResponseJson);
            }
            else
            {
                await WriteJsonAsync(context.Response, 200, ShapeResponse(tool, forwarded));
            }

            return;
        }

        if (request.HttpMethod != "POST" || remainder != "/")
        {
            await WriteJsonAsync(
                context.Response,
                404,
                JsonConvert.SerializeObject(new { error = "route not found" }));
            return;
        }

        TranspilePayload payload;
        try
        {
            payload = JsonConvert.DeserializeObject<TranspilePayload>(body ?? string.Empty)
                ?? throw new JsonException("empty payload");
        }
        catch (JsonException exception)
        {
            await WriteJsonAsync(
                context.Response,
                400,
                JsonConvert.SerializeObject(new { error = $"Request body is not a TranspilePayload: {exception.Message}" }));
            return;
        }

        if (string.IsNullOrEmpty(payload.CLIInputFileSetXml)
            && payload.TranspileRequest?.ZippedInputFileSet is { Length: > 0 } zipped)
        {
            payload.CLIInputFileSetXml = zipped.UnzipToString();
        }

        var result = await ScriptToolRunner.RunAsync(tool, payload, WorkRoot, _stopping.Token);
        await WriteJsonAsync(context.Response, 200, ShapeResponse(tool, result));
    }

    private ProxiedToolProcess GetProcess(LocalTool tool)
    {
        lock (_gate)
        {
            if (!_processes.TryGetValue(tool.Name, out var process))
            {
                process = new ProxiedToolProcess(tool, _fileSetHandlerPath, _debug);
                _processes[tool.Name] = process;
            }

            return process;
        }
    }

    private string DescribeTools()
    {
        var catalog = Catalog;
        return JsonConvert.SerializeObject(
            new
            {
                host = BaseUri.ToString(),
                projectRoot = catalog.ProjectRoot,
                tools = catalog.Tools.Select(tool => new
                {
                    name = tool.Name,
                    runtime = tool.RuntimeName,
                    url = ToolUri(tool.Name).ToString(),
                    description = tool.Description,
                    tags = tool.Tags,
                }),
                problems = catalog.Problems.Select(problem => new
                {
                    folder = problem.Folder,
                    reason = problem.Reason,
                }),
            },
            Formatting.Indented);
    }

    private static string ShapeResponse(LocalTool tool, LocalToolRunResult result)
    {
        var response = new JObject
        {
            ["TranspileRequest"] = new JObject
            {
                ["ZippedOutputFileSet"] = result.OutputFileSetXml is null
                    ? null
                    : Convert.ToBase64String(LocalFileSets.ZipFileSetXml(result.OutputFileSetXml)),
            },
            ["Transpiler"] = new JObject
            {
                ["Name"] = tool.Name,
                ["LowerHyphenName"] = tool.LedgerKey,
            },
            ["Logs"] = JArray.FromObject(result.Logs),
            ["Exception"] = result.ErrorMessage is null
                ? null
                : new JObject { ["Message"] = result.ErrorMessage },
        };
        return response.ToString(Formatting.None);
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, int statusCode, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
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

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
