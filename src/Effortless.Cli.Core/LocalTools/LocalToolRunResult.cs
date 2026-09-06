#nullable enable
namespace Effortless.Cli.LocalTools;

/// <summary>
/// What one local-tool execution produced, before it is shaped into the wire
/// response. <see cref="RawResponseJson"/> is set only for proxied (dotnet /
/// node) tools, whose response is forwarded verbatim.
/// </summary>
internal sealed class LocalToolRunResult
{
    public List<LogEntry> Logs { get; } = new();

    public string? ErrorMessage { get; set; }

    public string? OutputFileSetXml { get; set; }

    public int? StatusCode { get; set; }

    public string? RawResponseJson { get; set; }

    public bool Failed => ErrorMessage is not null;

    public static LocalToolRunResult Error(string message)
    {
        var result = new LocalToolRunResult { ErrorMessage = message };
        result.Logs.Add(new LogEntry("error", message));
        return result;
    }
}
