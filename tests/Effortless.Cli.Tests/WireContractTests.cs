using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using Effortless.Cli.FileSets;
using Effortless.Cli.Options;
using Newtonsoft.Json;

namespace Effortless.Cli.Tests;

public sealed class WireContractTests
{
    private const string InputXml =
        "<FileSet><FileSetFiles><FileSetFile><RelativePath>in.txt</RelativePath>"
        + "<ZippedFileContents>H4sIAAAAAAAAE8tIzcnJBwCGphA2BQAAAA==</ZippedFileContents>"
        + "</FileSetFile></FileSetFiles></FileSet>";

    [Fact(DisplayName = "wire-request-snapshot")]
    public async Task RequestJsonMatchesCheckedInSnapshot()
    {
        var requestJson = await CaptureRequestAsync();
        var root = JsonNode.Parse(requestJson)!.AsObject();
        var payloadId = root["payloadId"]!.GetValue<string>();
        var senderId = root["senderId"]!.GetValue<string>();

        Assert.Matches("^[0-9a-f]{64}$", payloadId);
        Assert.Matches("^[0-9a-f]{32}$", senderId);

        root["payloadId"] = "NORMALIZED_PAYLOAD_ID";
        root["senderId"] = "NORMALIZED_SENDER_ID";
        var zippedInput = Convert.FromBase64String(
            root["transpileRequest"]!["zippedInputFileSet"]!
                .GetValue<string>());
        Assert.Equal(InputXml, zippedInput.UnzipToString());
        if (zippedInput.Length >= 10)
        {
            // RFC 1952 byte 9 identifies the compressor OS but does not affect
            // the payload. Normalize it in the snapshot only; production gzip
            // bytes retain the runtime behavior tools depend on.
            zippedInput[9] = byte.MaxValue;
        }

        root["transpileRequest"]!["zippedInputFileSet"] =
            Convert.ToBase64String(zippedInput);

        Assert.Equal(
            Fixture("request.snapshot.json").TrimEnd(),
            root.ToJsonString());
    }

    [Fact(DisplayName = "wire-request-accepted-by-fileset-service")]
    public void SnapshotIsAcceptedByTheToolSideFileSetParser()
    {
        var fileSet = ToolSideToFileSet(
            Fixture("request.snapshot.json"));

        var file = Assert.Single(fileSet.FileSetFiles);
        Assert.Equal("in.txt", file.RelativePath);
        Assert.Equal("hello", file.ZippedFileContents.UnzipToString());
    }

    [Fact(DisplayName = "wire-response-variants")]
    public async Task ResponseVariantsMatchTheWireContract()
    {
        var zippedOutput = Convert.ToBase64String(OutputXml().Zip());

        var pascal = await ParseResponseAsync(
            System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    Transpiler = new { Name = "pascal" },
                    Logs = Array.Empty<object>(),
                    TranspileRequest = new
                    {
                        ZippedOutputFileSet = zippedOutput,
                    },
                }));
        Assert.True(pascal.Succeeded);
        Assert.Equal("pascal", pascal.Payload.Transpiler.Name);
        Assert.Equal(TranspileOutputDisposition.Save, pascal.OutputDisposition);

        var camel = await ParseResponseAsync(
            """{"transpiler":{"name":"camel"},"logs":null,"transpileRequest":{}}""");
        Assert.True(camel.Succeeded);
        Assert.Equal("camel", camel.Payload.Transpiler.Name);
        Assert.Null(camel.Payload.Logs);

        var missingTranspiler = await ParseResponseAsync(
            """{"logs":null,"transpileRequest":{}}""");
        Assert.True(missingTranspiler.Succeeded);
        Assert.Equal("wire-tool", missingTranspiler.Payload.Transpiler.Name);

        var exception = await ParseResponseAsync(
            """{"Exception":{"Message":"outer","StackTrace":"at Remote.Run()","InnerException":{"Message":"inner"}}}""");
        Assert.False(exception.Succeeded);
        Assert.Equal("outer", exception.Payload.Exception.Message);
        Assert.Equal("inner", exception.Payload.Exception.InnerException?.Message);
        Assert.Equal("at Remote.Run()", exception.Payload.Exception.StackTrace);

        var pendingHandler = new RecordingHandler(
            _ => JsonResponse(
                new
                {
                    taskId = "task-wire",
                    taskStatus = "pending",
                }),
            _ => JsonResponse(
                new
                {
                    taskId = "task-wire",
                    taskStatus = "completed",
                    transpileRequest = new
                    {
                        zippedOutputFileSet = zippedOutput,
                    },
                }));
        using var httpClient = new HttpClient(pendingHandler);
        using var client = new TranspileClient(
            httpClient,
            (_, _) => Task.CompletedTask);

        var pending = await client.TranspileAsync(Invocation());

        Assert.True(pending.Succeeded);
        Assert.Equal("completed", pending.Payload.TaskStatus);
        Assert.Equal(TranspileOutputDisposition.Save, pending.OutputDisposition);
        Assert.Equal(
            "https://tools.invalid/run/task/task-wire",
            pendingHandler.RequestUris.Last().ToString());
    }

    private static async Task<string> CaptureRequestAsync()
    {
        var handler = new RecordingHandler(
            _ => JsonResponse(
                new
                {
                    transpiler = new { name = "wire-tool" },
                    logs = Array.Empty<object>(),
                    transpileRequest = new
                    {
                        zippedOutputFileSet =
                            Convert.ToBase64String(OutputXml().Zip()),
                    },
                }));
        var entityIds = new Queue<Guid>(
        [
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
        ]);
        using var httpClient = new HttpClient(handler);
        using var client = new TranspileClient(
            httpClient,
            () => entityIds.Dequeue());

        var result = await client.TranspileAsync(Invocation());

        Assert.True(result.Succeeded);
        return Assert.Single(handler.RequestBodies);
    }

    private static async Task<TranspileClientResult> ParseResponseAsync(
        string responseJson)
    {
        var handler = new RecordingHandler(
            _ => Response(HttpStatusCode.OK, responseJson));
        using var httpClient = new HttpClient(handler);
        using var client = new TranspileClient(httpClient);
        return await client.TranspileAsync(Invocation());
    }

    private static CliInvocation Invocation() =>
        new()
        {
            Options = new CliOptions
            {
                input = ["in.txt"],
                output = "out.txt",
                parameters = ["k=v", "param1=extra", "project-name=wire"],
                waitTimeout = 12_345,
            },
            RawTranspilerArg = "wire-tool",
            TargetUrl = "https://tools.invalid/run/",
            Transpiler = "wire-tool",
            Account = "acme",
            InputFileSetXml = InputXml,
            Jwt = "jwt",
            CurrentDirectory = "/work",
        };

    private static ToolSideFileSet ToolSideToFileSet(string requestJson)
    {
        var request = JsonConvert.DeserializeObject<ToolSideRequest>(
            requestJson)
            ?? throw new InvalidDataException(
                "The wire snapshot did not deserialize.");
        var zippedInput = request.TranspileRequest?.ZippedInputFileSet
            ?? throw new InvalidDataException(
                "The wire snapshot has no zipped input FileSet.");
        var fileSetXml = zippedInput.UnzipToString();
        fileSetXml = fileSetXml[fileSetXml.IndexOf('<')..];

        // Ported from CLIClassLibrary's FileSetService.ToFileSet path:
        // base64 JSON -> gzip XML -> manually populated FileSet records.
        var document = new XmlDocument();
        document.LoadXml(fileSetXml);
        var fileSet = new ToolSideFileSet();
        foreach (XmlElement fileNode in
                 document.SelectNodes("//FileSetFile")!)
        {
            var file = new ToolSideFile
            {
                RelativePath =
                    fileNode.SelectSingleNode("RelativePath")?.InnerText,
            };
            var zippedContents =
                fileNode.SelectSingleNode("ZippedFileContents")?.InnerText;
            if (!string.IsNullOrEmpty(zippedContents))
            {
                file.ZippedFileContents =
                    Convert.FromBase64String(zippedContents);
            }

            fileSet.FileSetFiles.Add(file);
        }

        return fileSet;
    }

    private static string Fixture(string name) =>
        File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "fixtures",
                "wire",
                name));

    private static string OutputXml() =>
        "<FileSet><FileSetFiles><FileSetFile><RelativePath>out.txt</RelativePath>"
        + "<FileContents>done</FileContents></FileSetFile></FileSetFiles></FileSet>";

    private static HttpResponseMessage JsonResponse(object value) =>
        Response(
            HttpStatusCode.OK,
            System.Text.Json.JsonSerializer.Serialize(value));

    private static HttpResponseMessage Response(
        HttpStatusCode statusCode,
        string body) =>
        new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>>
            _responses;

        public RecordingHandler(
            params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
        {
            _responses =
                new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(
                    responses);
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
                    : await request.Content.ReadAsStringAsync(
                        cancellationToken));
            if (!_responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException(
                    "No scripted HTTP response remains.");
            }

            return response(request);
        }
    }

    private sealed class ToolSideRequest
    {
        public ToolSideTranspileRequest? TranspileRequest { get; set; }
    }

    private sealed class ToolSideTranspileRequest
    {
        public byte[]? ZippedInputFileSet { get; set; }
    }

    private sealed class ToolSideFileSet
    {
        public List<ToolSideFile> FileSetFiles { get; } = [];
    }

    private sealed class ToolSideFile
    {
        public string? RelativePath { get; set; }

        public byte[] ZippedFileContents { get; set; } = [];
    }
}
