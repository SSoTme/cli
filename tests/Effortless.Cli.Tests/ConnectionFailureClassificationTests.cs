using System.Net;
using System.Net.Sockets;
using Effortless.Cli.Options;

namespace Effortless.Cli.Tests;

/// <summary>
/// R11 trigger classification: which failures count as a "complete timeout"
/// (workload never answered) versus an answer the RetryRules matrix owns.
/// </summary>
public sealed class ConnectionFailureClassificationTests
{
    [Fact(DisplayName = "unit-r11-classification: connection refused, host not found, 502 answers, and silent sockets are classified per D29")]
    public async Task ClassifiesFailuresPerD29()
    {
        Assert.Equal(
            TranspileConnectionFailure.ConnectionRefused,
            await RunAgainst(new HttpRequestException(
                "refused",
                new SocketException((int)SocketError.ConnectionRefused))));
        Assert.Equal(
            TranspileConnectionFailure.HostNotFound,
            await RunAgainst(new HttpRequestException(
                "No such host is known",
                new SocketException((int)SocketError.HostNotFound))));
        Assert.Equal(
            TranspileConnectionFailure.TlsHandshake,
            await RunAgainst(new HttpRequestException(
                "The SSL connection could not be established",
                new System.Security.Authentication.AuthenticationException("tls"))));

        // A tool that answers, even with a gateway status forever, is reachable.
        var answered = await RunWith(new StaticHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("bad gateway"),
            }));
        Assert.False(answered.Succeeded);
        Assert.Equal(TranspileConnectionFailure.None, answered.ConnectionFailure);
        Assert.False(answered.IsCompleteTimeout);

        // A socket that accepts and never writes bytes is a complete timeout.
        var silent = await RunWith(new StaticHandler(async cancellation =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        Assert.False(silent.Succeeded);
        Assert.Equal(TranspileConnectionFailure.NoResponseWithinTimeout, silent.ConnectionFailure);
        Assert.True(silent.IsCompleteTimeout);
    }

    private static async Task<TranspileConnectionFailure> RunAgainst(HttpRequestException exception)
    {
        var result = await RunWith(new StaticHandler((CancellationToken _) => Task.FromException<HttpResponseMessage>(exception)));
        Assert.False(result.Succeeded);
        Assert.True(result.IsCompleteTimeout);
        return result.ConnectionFailure;
    }

    private static async Task<TranspileClientResult> RunWith(HttpMessageHandler handler)
    {
        using var httpClient = new HttpClient(handler);
        using var client = new TranspileClient(
            httpClient,
            (_, _) => Task.CompletedTask);
        return await client.TranspileAsync(new CliInvocation
        {
            Options = new CliOptions
            {
                input = ["in.txt"],
                waitTimeout = 1_500,
            },
            RawTranspilerArg = "wire-tool",
            Transpiler = "wire-tool",
            TargetUrl = "https://tools.invalid/run",
            InputFileSetXml = "<FileSet><FileSetFiles></FileSetFiles></FileSet>",
            CurrentDirectory = Directory.GetCurrentDirectory(),
        });
    }

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly Func<CancellationToken, Task<HttpResponseMessage>> _respond;

        public StaticHandler(Func<CancellationToken, HttpResponseMessage> respond)
            : this(cancellation => Task.FromResult(respond(cancellation)))
        {
        }

        public StaticHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _respond(cancellationToken);
    }
}
