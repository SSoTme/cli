#nullable enable
using System.Diagnostics;
using System.Net.Sockets;
using Newtonsoft.Json;

namespace Effortless.Cli.LocalTools;

/// <summary>
/// <c>&lt;root&gt;/.effortless/serve.json</c>: the resident host started by
/// <c>effortless serve</c>, so builds in the same project reuse it instead of
/// starting an ephemeral one. A stale file (dead pid, nothing listening) is
/// ignored.
/// </summary>
public sealed class ServeState
{
    public const string FileName = "serve.json";

    [JsonProperty("port")]
    public int Port { get; set; }

    [JsonProperty("pid")]
    public int Pid { get; set; }

    [JsonProperty("startedAt")]
    public DateTimeOffset StartedAt { get; set; }

    public static string PathFor(string projectRoot) =>
        Path.Combine(projectRoot, Project.EffortlessProject.LedgerDirectoryName, FileName);

    public static void Write(string projectRoot, int port)
    {
        var path = PathFor(projectRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            JsonConvert.SerializeObject(
                new ServeState
                {
                    Port = port,
                    Pid = Environment.ProcessId,
                    StartedAt = DateTimeOffset.UtcNow,
                },
                Formatting.Indented) + Environment.NewLine);
    }

    public static void Delete(string projectRoot)
    {
        try
        {
            var path = PathFor(projectRoot);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort.
        }
    }

    /// <summary>
    /// Returns the live resident host for the project, or null when none is
    /// recorded, the recorded pid is dead, or nothing accepts connections on
    /// the recorded port.
    /// </summary>
    public static ServeState? TryReadLive(string projectRoot)
    {
        var path = PathFor(projectRoot);
        if (!File.Exists(path))
        {
            return null;
        }

        ServeState? state;
        try
        {
            state = JsonConvert.DeserializeObject<ServeState>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }

        if (state is null || state.Port <= 0 || !IsProcessAlive(state.Pid))
        {
            return null;
        }

        try
        {
            using var probe = new TcpClient();
            var connect = probe.ConnectAsync(System.Net.IPAddress.Loopback, state.Port);
            return connect.Wait(TimeSpan.FromSeconds(1)) ? state : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        if (pid <= 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
