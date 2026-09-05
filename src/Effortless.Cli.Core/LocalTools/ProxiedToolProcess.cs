#nullable enable
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;

namespace Effortless.Cli.LocalTools;

/// <summary>
/// A dotnet or node tool running as a child process on its own loopback
/// port, exactly the shape of a published cloud workload (reads PORT,
/// answers POST /). The host forwards requests to it verbatim.
/// </summary>
internal sealed class ProxiedToolProcess : IDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);

    private readonly LocalTool _tool;
    private readonly string? _fileSetHandlerPath;
    private readonly Action<string>? _debug;
    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private readonly StringBuilder _startupOutput = new();
    private Process? _process;
    private bool _disposed;

    public ProxiedToolProcess(
        LocalTool tool,
        string? fileSetHandlerPath,
        Action<string>? debug)
    {
        _tool = tool;
        _fileSetHandlerPath = fileSetHandlerPath;
        _debug = debug;
    }

    public int Port { get; private set; }

    public Uri? BaseUri { get; private set; }

    public bool IsRunning => _process is { HasExited: false };

    /// <summary>
    /// Starts the tool process if needed and forwards one HTTP request to it.
    /// </summary>
    public async Task<LocalToolRunResult> ForwardAsync(
        string method,
        string pathAndQuery,
        string? body,
        CancellationToken cancellationToken)
    {
        var startError = await EnsureStartedAsync(cancellationToken);
        if (startError is not null)
        {
            return LocalToolRunResult.Error(startError);
        }

        using var request = new HttpRequestMessage(
            new HttpMethod(method),
            new Uri(BaseUri!, pathAndQuery.TrimStart('/')));
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8);
            request.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        }

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            return new LocalToolRunResult
            {
                StatusCode = (int)response.StatusCode,
                RawResponseJson = await response.Content.ReadAsStringAsync(cancellationToken),
            };
        }
        catch (HttpRequestException exception)
        {
            var exited = _process is { HasExited: true };
            return LocalToolRunResult.Error(
                exited
                    ? $"Local tool '{_tool.Name}' exited with code {_process!.ExitCode}."
                    : $"Local tool '{_tool.Name}' did not answer at {BaseUri}: {exception.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _http.Dispose();
        _startGate.Dispose();
    }

    public void Stop()
    {
        var process = _process;
        _process = null;
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch
        {
            // Best effort.
        }
        finally
        {
            process.Dispose();
        }
    }

    private async Task<string?> EnsureStartedAsync(CancellationToken cancellationToken)
    {
        await _startGate.WaitAsync(cancellationToken);
        try
        {
            if (IsRunning)
            {
                return null;
            }

            Stop();
            Port = FreePort();
            BaseUri = new Uri($"http://127.0.0.1:{Port}/");
            _startupOutput.Clear();

            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = _tool.Directory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            string executable;
            if (_tool.Runtime == LocalToolRuntime.Dotnet)
            {
                executable = "dotnet";
                startInfo.FileName = "dotnet";
                startInfo.ArgumentList.Add("run");
                startInfo.ArgumentList.Add("--project");
                startInfo.ArgumentList.Add(_tool.Entry);
                startInfo.ArgumentList.Add("--no-launch-profile");
                // A cloud tool's own dev launch settings must not override PORT.
                startInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{Port}";
            }
            else
            {
                executable = "node";
                startInfo.FileName = "node";
                startInfo.ArgumentList.Add(_tool.Entry);
                if (_fileSetHandlerPath is not null)
                {
                    startInfo.Environment["EFFORTLESS_FILESET_HANDLER"] = _fileSetHandlerPath;
                }
            }

            startInfo.Environment["PORT"] = Port.ToString();
            startInfo.Environment["EFFORTLESS_TOOL_PORT"] = Port.ToString();
            startInfo.Environment["EFFORTLESS_TOOL_NAME"] = _tool.Name;
            startInfo.Environment.Remove("EFFORTLESS_CHILD_PROCESS");

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => Capture(e.Data);
            process.ErrorDataReceived += (_, e) => Capture(e.Data);
            try
            {
                process.Start();
            }
            catch (Win32Exception)
            {
                process.Dispose();
                return $"ERROR: Local tool '{_tool.Name}' needs the '{_tool.RuntimeName}' runtime ('{executable}') which was not found on PATH.";
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _process = process;
            _debug?.Invoke($"DEBUG: started {_tool.RuntimeName} tool '{_tool.Name}' (pid {process.Id}) on {BaseUri}");

            var readyError = await WaitUntilReadyAsync(process, cancellationToken);
            if (readyError is not null)
            {
                Stop();
            }

            return readyError;
        }
        finally
        {
            _startGate.Release();
        }
    }

    private async Task<string?> WaitUntilReadyAsync(Process process, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + StartupTimeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                return $"Local tool '{_tool.Name}' exited with code {process.ExitCode} before it started listening.{StartupOutputSuffix()}";
            }

            try
            {
                using var probe = new TcpClient();
                using var probeTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                await probe.ConnectAsync(IPAddress.Loopback, Port, probeTimeout.Token);
                return null;
            }
            catch (Exception exception) when (exception is SocketException or OperationCanceledException)
            {
                await Task.Delay(250, cancellationToken);
            }
        }

        return $"Local tool '{_tool.Name}' did not start listening on port {Port} within {StartupTimeout.TotalSeconds:0} s.{StartupOutputSuffix()}";
    }

    private void Capture(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (_startupOutput)
        {
            if (_startupOutput.Length < 16_000)
            {
                _startupOutput.AppendLine(line);
            }
        }

        _debug?.Invoke($"[{_tool.Name}] {line}");
    }

    private string StartupOutputSuffix()
    {
        lock (_startupOutput)
        {
            return _startupOutput.Length == 0
                ? string.Empty
                : Environment.NewLine + _startupOutput.ToString().TrimEnd();
        }
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
