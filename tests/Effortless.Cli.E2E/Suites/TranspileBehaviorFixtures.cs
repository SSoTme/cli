using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

internal sealed class FailedAsyncTerminalBehavior(string message) : ToolBehavior
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
                    exception: SerializedExceptionPayload.Create(
                        message,
                        "async failure stack",
                        null),
                    taskStatus: "failed")));
}

internal sealed class ExceptionChainBehavior(
    string message,
    string innerMessage,
    string stackTrace) : ToolBehavior
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
                    exception: SerializedExceptionPayload.Create(
                        message,
                        stackTrace,
                        SerializedExceptionPayload.Create(innerMessage, "at Mock.Inner()", null)),
                    taskStatus: taskStatus)));
}

internal static class SerializedExceptionPayload
{
    public static object Create(
        string exceptionMessage,
        string exceptionStackTrace,
        object? innerException) =>
        new
        {
            ClassName = "System.Exception",
            Message = exceptionMessage,
            Data = (object?)null,
            InnerException = innerException,
            HelpURL = (string?)null,
            StackTraceString = exceptionStackTrace,
            RemoteStackTraceString = (string?)null,
            RemoteStackIndex = 0,
            ExceptionMethod = (object?)null,
            HResult = unchecked((int)0x80131500),
            Source = (string?)null,
            WatsonBuckets = (object?)null,
        };
}
