using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Effortless.Cli.FileSets;
using Effortless.Cli.Options;

namespace Effortless.Cli.Tests;

public sealed class TranspileClientTests
{
    [Fact(DisplayName = "unit-transpile-wire: REST request has the exact camelCase wire shape")]
    public async Task RestRequestHasExactCamelCaseWireShape()
    {
        const string inputXml =
            "<FileSet><FileSetFiles><FileSetFile><RelativePath>in.txt</RelativePath>"
            + "<ZippedFileContents>H4sIAAAAAAAAE8tIzcnJBwCGphA2BQAAAA==</ZippedFileContents>"
            + "</FileSetFile></FileSetFiles></FileSet>";
        var handler = new RecordingHandler(
            _ => JsonResponse(SuccessPayload("")));
        using var httpClient = new HttpClient(handler);
        using var client = new TranspileClient(httpClient);
        var invocation = Invocation(inputXml);

        var result = await client.TranspileAsync(invocation);

        Assert.True(result.Succeeded);
        Assert.Equal(TranspileOutputDisposition.Save, result.OutputDisposition);
        Assert.Equal(
            "httpstoolsinvalidrun",
            result.TranspilerKey);
        Assert.Equal("httpstoolsinvalidrun", result.Payload.Transpiler.Name);
        var requestJson = Assert.Single(handler.RequestBodies);
        using var document = JsonDocument.Parse(requestJson);
        var root = document.RootElement;

        Assert.Equal(
            [
                "payloadId",
                "senderId",
                "settings",
                "transpiler",
                "transpileRequest",
                "cliAccount",
                "cliInput",
                "cliInputFileContents",
                "cliInputFileSetJson",
                "cliInputFileSetXml",
                "cliOutput",
                "cliParams",
                "cliTranspiler",
                "cliWaitTimeout",
                "cliDebug",
                "cliJwt",
            ],
            root.EnumerateObject().Select(property => property.Name).ToArray());

        Assert.Matches("^[0-9a-f]{64}$", root.GetProperty("payloadId").GetString());
        Assert.Matches("^[0-9a-f]{32}$", root.GetProperty("senderId").GetString());
        Assert.Empty(root.GetProperty("settings").EnumerateObject());
        Assert.Equal(
            ["in.txt"],
            Strings(root.GetProperty("cliInput")));
        Assert.Equal(
            ["k=v", "param1=extra", "project-name=wire"],
            Strings(root.GetProperty("cliParams")));
        Assert.Equal(string.Empty, root.GetProperty("cliInputFileContents").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("cliInputFileSetJson").ValueKind);
        Assert.Equal(inputXml, root.GetProperty("cliInputFileSetXml").GetString());
        Assert.Equal("out.txt", root.GetProperty("cliOutput").GetString());
        Assert.Equal("acme", root.GetProperty("cliAccount").GetString());
        Assert.Equal("httpstoolsinvalidrun", root.GetProperty("cliTranspiler").GetString());
        Assert.Equal(12_345, root.GetProperty("cliWaitTimeout").GetInt32());
        Assert.False(root.GetProperty("cliDebug").GetBoolean());
        Assert.Equal("jwt", root.GetProperty("cliJwt").GetString());

        var transpiler = root.GetProperty("transpiler");
        Assert.Equal("httpstoolsinvalidrun", transpiler.GetProperty("name").GetString());
        Assert.Equal(
            "httpstoolsinvalidrun",
            transpiler.GetProperty("lowerHyphenName").GetString());
        var zippedInput = Convert.FromBase64String(
            root.GetProperty("transpileRequest")
                .GetProperty("zippedInputFileSet")
                .GetString()!);
        Assert.Equal(inputXml, zippedInput.UnzipToString());
    }

    [Fact(DisplayName = "unit-transpile-retries: gateway and SSL-body retries use rulebook delays")]
    public async Task GatewayAndSslBodyRetriesUseRulebookDelays()
    {
        var handler = new RecordingHandler(
            _ => Response(HttpStatusCode.ServiceUnavailable, """{"error":"booting"}"""),
            _ => Response(HttpStatusCode.BadRequest, """{"error":"SSL connection could not be established"}"""),
            _ => JsonResponse(SuccessPayload("echo")));
        using var httpClient = new HttpClient(handler);
        var delays = new List<TimeSpan>();
        using var client = new TranspileClient(
            httpClient,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        var result = await client.TranspileAsync(Invocation());

        Assert.True(result.Succeeded);
        Assert.Equal(3, handler.RequestBodies.Count);
        Assert.Equal(
            [RetryPolicy.GatewayDelay, RetryPolicy.ConnectionDelay],
            delays);
    }

    [Fact(DisplayName = "unit-transpile-async: pending tasks poll every three seconds")]
    public async Task PendingTasksPollEveryThreeSeconds()
    {
        var pollCount = 0;
        var handler = new RecordingHandler(
            request =>
            {
                if (request.Method == HttpMethod.Post)
                {
                    return JsonResponse(
                        new
                        {
                            TaskId = "task-1",
                            TaskStatus = "pending",
                            Transpiler = new { Name = "echo" },
                        });
                }

                pollCount++;
                return pollCount == 1
                    ? JsonResponse(
                        new
                        {
                            TaskId = "task-1",
                            TaskStatus = "pending",
                            Transpiler = new { Name = "echo" },
                        })
                    : JsonResponse(
                        new
                        {
                            TaskId = "task-1",
                            TaskStatus = "completed",
                            Transpiler = new { Name = "echo" },
                            TranspileRequest = new
                            {
                                ZippedOutputFileSet =
                                    Convert.ToBase64String(OutputXml().Zip()),
                            },
                        });
            });
        using var httpClient = new HttpClient(handler);
        var delays = new List<TimeSpan>();
        using var client = new TranspileClient(
            httpClient,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        var result = await client.TranspileAsync(Invocation());

        Assert.True(result.Succeeded);
        Assert.Equal(2, pollCount);
        Assert.Equal(
            [RetryPolicy.AsyncPollDelay, RetryPolicy.AsyncPollDelay],
            delays);
        Assert.Equal(
            "https://tools.invalid/run/task/task-1",
            handler.RequestUris.Last().ToString());
    }

    [Fact(DisplayName = "unit-transpile-errors: response exceptions and error logs are promoted")]
    public async Task ResponseExceptionsAndErrorLogsArePromoted()
    {
        var handler = new RecordingHandler(
            _ => JsonResponse(
                new
                {
                    Transpiler = new { Name = "echo" },
                    Exception = new
                    {
                        Message = "outer failure",
                        StackTrace = "at Mock.Transpiler()",
                        InnerException = new { Message = "inner failure" },
                    },
                }),
            _ => JsonResponse(
                new
                {
                    Transpiler = new { Name = "echo" },
                    Logs = new[]
                    {
                        new { Level = "error", Text = "boom" },
                    },
                }));
        using var httpClient = new HttpClient(handler);
        using var client = new TranspileClient(httpClient);

        var exceptionResult = await client.TranspileAsync(Invocation());
        var logResult = await client.TranspileAsync(Invocation());

        Assert.False(exceptionResult.Succeeded);
        Assert.Equal("outer failure", exceptionResult.Payload.Exception.Message);
        Assert.Equal(
            "inner failure",
            exceptionResult.Payload.Exception.InnerException?.Message);
        Assert.Equal(
            "at Mock.Transpiler()",
            exceptionResult.Payload.Exception.StackTrace);
        Assert.False(logResult.Succeeded);
        Assert.Equal("boom", logResult.Payload.Exception.Message);
        Assert.Equal(TranspileOutputDisposition.None, logResult.OutputDisposition);
    }

    [Fact(DisplayName = "unit-retry-classifier: connection refusal aborts on the third occurrence")]
    public void ConnectionRefusalAbortsOnThirdOccurrence()
    {
        var policy = new RetryPolicy();
        var exception = new HttpRequestException(
            "refused",
            new SocketException((int)SocketError.ConnectionRefused));
        var target = new Uri("http://127.0.0.1:1/");

        var first = policy.Classify(exception, target, "dead", 1, 1);
        var third = policy.Classify(exception, target, "dead", 3, 3);

        Assert.True(first.ShouldRetry);
        Assert.False(first.ShouldAbort);
        Assert.Equal(RetryPolicy.ConnectionDelay, first.Delay);
        Assert.False(third.ShouldRetry);
        Assert.True(third.ShouldAbort);
        Assert.Contains(
            "Connection refused 3 times for: http://127.0.0.1:1/",
            third.Message,
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "unit-transpile-no-target: missing target URLs fail before HTTP")]
    public async Task MissingTargetUrlFailsBeforeHttp()
    {
        var handler = new RecordingHandler(
            _ => throw new InvalidOperationException("HTTP must not be called."));
        using var httpClient = new HttpClient(handler);
        using var client = new TranspileClient(httpClient);
        var invocation = Invocation();
        invocation.TargetUrl = null;

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => client.TranspileAsync(invocation));

        Assert.Contains("no target URL", exception.Message, StringComparison.Ordinal);
        Assert.Empty(handler.RequestBodies);
    }

    [Fact(DisplayName = "unit-transpile-timeout: injected delay and clock make wait bounds deterministic")]
    public async Task InjectedDelayAndClockMakeWaitBoundsDeterministic()
    {
        var handler = new RecordingHandler(
            _ => throw new TaskCanceledException("simulated HTTP timeout"));
        using var httpClient = new HttpClient(handler);
        var clock = new ManualTimeProvider();
        var delays = new List<TimeSpan>();
        using var client = new TranspileClient(
            httpClient,
            (delay, _) =>
            {
                delays.Add(delay);
                clock.Advance(delay);
                return Task.CompletedTask;
            },
            clock);
        var invocation = Invocation();
        invocation.Options.waitTimeout = 1_500;

        var result = await client.TranspileAsync(invocation);

        Assert.False(result.Succeeded);
        Assert.IsType<TimeoutException>(result.Payload.Exception);
        Assert.Equal(
            "Timed out waiting for cook",
            result.Payload.Exception.Message);
        Assert.Equal(TimeSpan.FromMilliseconds(1_500), delays.Aggregate(TimeSpan.Zero, (sum, delay) => sum + delay));
    }

    private static CliInvocation Invocation(string inputXml = "<FileSet><FileSetFiles /></FileSet>") =>
        new()
        {
            Options = new CliOptions
            {
                input = ["in.txt"],
                output = "out.txt",
                parameters = ["k=v", "param1=extra", "project-name=wire"],
                waitTimeout = 12_345,
            },
            RawTranspilerArg = "echo",
            TargetUrl = "https://tools.invalid/run/",
            Transpiler = "httpstoolsinvalidrun",
            Account = "acme",
            InputFileSetXml = inputXml,
            Jwt = "jwt",
            CurrentDirectory = "/work",
        };

    private static object SuccessPayload(string toolName) =>
        new
        {
            TranspileRequest = new
            {
                ZippedOutputFileSet =
                    Convert.ToBase64String(OutputXml().Zip()),
            },
            Transpiler = new
            {
                Name = toolName,
            },
            Logs = Array.Empty<object>(),
        };

    private static string OutputXml() =>
        "<FileSet><FileSetFiles><FileSetFile><RelativePath>out.txt</RelativePath>"
        + "<FileContents>done</FileContents></FileSetFile></FileSetFiles></FileSet>";

    private static HttpResponseMessage JsonResponse(object value) =>
        Response(HttpStatusCode.OK, JsonSerializer.Serialize(value));

    private static HttpResponseMessage Response(
        HttpStatusCode statusCode,
        string body) =>
        new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private static string[] Strings(JsonElement array) =>
        array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>>
            _responses;
        private readonly Func<HttpRequestMessage, HttpResponseMessage>?
            _response;

        public RecordingHandler(
            params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
        {
            _responses = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(
                responses);
        }

        public RecordingHandler(
            Func<HttpRequestMessage, HttpResponseMessage> response)
        {
            _responses = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>();
            _response = response;
        }

        public List<string> RequestBodies { get; } = [];

        public List<Uri> RequestUris { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            RequestBodies.Add(
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken));

            if (_response is not null)
            {
                return _response(request);
            }

            if (!_responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException(
                    "No scripted HTTP response remains.");
            }

            return response(request);
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan duration)
        {
            _timestamp += duration.Ticks;
        }
    }
}
