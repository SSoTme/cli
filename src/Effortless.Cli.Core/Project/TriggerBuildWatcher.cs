using Newtonsoft.Json.Linq;

namespace Effortless.Cli.Project;

public sealed class TriggerBuildWatcher
{
    public const string DefaultBaseUri =
        "https://ssotme-cli-airtable-bridge-ahrnz660db6k4.aws-us-east-1.controlplane.us";

    private static readonly TimeSpan PollInterval =
        TimeSpan.FromSeconds(3);
    private static readonly TimeSpan QuietPeriod =
        TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly Func<TimeSpan, CancellationToken, Task>
        _delayAsync;
    private readonly Action<string> _writeLine;

    public TriggerBuildWatcher(
        HttpClient httpClient = null,
        TimeProvider timeProvider = null,
        Func<TimeSpan, CancellationToken, Task>
            delayAsync = null,
        Action<string> writeLine = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _timeProvider =
            timeProvider ?? TimeProvider.System;
        _delayAsync = delayAsync
                      ?? ((delay, token) =>
                          Task.Delay(delay, token));
        _writeLine = writeLine ?? Console.WriteLine;
    }

    public async Task WatchAsync(
        string baseId,
        Func<Task> rebuildAsync,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseId))
        {
            throw new ArgumentException(
                "A non-empty baseId is required.",
                nameof(baseId));
        }

        ArgumentNullException.ThrowIfNull(rebuildAsync);
        var checkUri =
            $"{DefaultBaseUri}/check?baseId={Uri.EscapeDataString(baseId)}";
        _writeLine(
            $"Polling {checkUri} for changes to base: `{baseId}`...");
        DateTimeOffset? lastChange = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var response = await _httpClient.GetAsync(
                checkUri,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(
                cancellationToken);
            var root = JObject.Parse(body);
            var changed = root["changed"]?.Value<bool?>()
                          ?? throw new InvalidDataException(
                              "Trigger response is missing boolean 'changed'.");
            var now = _timeProvider.GetUtcNow();
            if (changed)
            {
                lastChange = now;
                _writeLine(
                    $"Change detected at {now.ToLocalTime():HH:mm:ss}");
            }
            else if (lastChange.HasValue
                     && now - lastChange.Value
                     >= QuietPeriod)
            {
                _writeLine(
                    "No changes in the last 10 seconds. Rebuilding...");
                await rebuildAsync();
                lastChange = null;
            }

            await _delayAsync(
                PollInterval,
                cancellationToken);
        }
    }
}
