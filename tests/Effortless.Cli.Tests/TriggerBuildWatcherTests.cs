using System.Net;
using System.Text;
using Effortless.Cli.Project;

namespace Effortless.Cli.Tests;

public sealed class TriggerBuildWatcherTests
{
    [Fact(DisplayName = "unit-build-on-trigger: quiet period rebuilds and transport errors fail")]
    public async Task RebuildsAfterTenQuietSeconds()
    {
        var responses = new Queue<bool>(
            [true, false, false, false, false]);
        using var httpClient = new HttpClient(
            new TriggerHandler(responses));
        var clock = new ManualTimeProvider(
            new DateTimeOffset(
                2026,
                8,
                31,
                12,
                0,
                0,
                TimeSpan.Zero));
        using var cancellation = new CancellationTokenSource();
        var rebuilds = 0;
        var watcher = new TriggerBuildWatcher(
            httpClient,
            clock,
            (delay, _) =>
            {
                clock.Advance(delay);
                return Task.CompletedTask;
            },
            _ => { });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => watcher.WatchAsync(
                "app123",
                () =>
                {
                    rebuilds++;
                    cancellation.Cancel();
                    return Task.CompletedTask;
                },
                cancellation.Token));

        Assert.Equal(1, rebuilds);
    }

    [Fact]
    public async Task HttpFailuresStopTheWatcher()
    {
        using var httpClient = new HttpClient(
            new FailingHandler());
        var watcher = new TriggerBuildWatcher(
            httpClient,
            writeLine: _ => { });

        await Assert.ThrowsAsync<HttpRequestException>(
            () => watcher.WatchAsync(
                "app123",
                () => Task.CompletedTask));
    }

    private sealed class TriggerHandler : HttpMessageHandler
    {
        private readonly Queue<bool> _responses;

        public TriggerHandler(Queue<bool> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var changed = _responses.Dequeue()
                ? "true"
                : "false";
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $$"""{"changed":{{changed}}}""",
                        Encoding.UTF8,
                        "application/json"),
                });
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.ServiceUnavailable));
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() =>
            _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
        }
    }
}
