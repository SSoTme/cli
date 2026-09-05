using Effortless.Cli.Project;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli.Tests;

public sealed class BuildErrorLogTests
{
    [Fact(DisplayName = "unit-build-error-log: BuildErrorLog emits the public v1 document")]
    public void BuildErrorLogEmitsThePublicV1Document()
    {
        using var directory = new TestDirectory();
        var succeeded = Step("Succeeded");
        var failed = Step("Failed");
        var skipped = Step("Skipped");

        var log = new BuildErrorLog();
        log.Begin(directory.Path, continueOnError: true, buildCommand: "build");
        log.RecordSuccess(succeeded);
        log.RecordFailure(
            failed,
            exitCode: 7,
            transpilerException: new InvalidOperationException(
                "outer",
                new ArgumentException("inner")),
            thrownException: null,
            resolvedVersion: "v2026.08.30.1725",
            resolvedUrl: "https://example.test/tool/");
        log.RecordSkipped(skipped, "disabled");

        var reportPath = log.Finish();

        Assert.Equal(directory.File(BuildErrorLog.ErrorsFileName), reportPath);
        var report = JObject.Parse(File.ReadAllText(reportPath!));
        Assert.Equal(BuildErrorLog.SchemaId, (string?)report["schema"]);
        Assert.Equal("build", (string?)report["buildCommand"]);
        Assert.True((bool?)report["continueOnError"]);
        Assert.Equal(3, (int?)report["totalSteps"]);
        Assert.Equal(1, (int?)report["succeededSteps"]);
        Assert.Equal(1, (int?)report["failedSteps"]);
        Assert.Equal(1, (int?)report["skippedSteps"]);
        Assert.Equal(["Failed"], report["failedStepNames"]!.Values<string>().ToArray());
        Assert.Equal(
            ["succeeded", "failed", "skipped"],
            report["steps"]!.Values<string>("status").ToArray());

        var error = Assert.IsType<JObject>(Assert.Single((JArray)report["errors"]!));
        Assert.Equal(7, (int?)error["exitCode"]);
        Assert.Equal("v2026.08.30.1725", (string?)error["resolvedVersion"]);
        Assert.Equal("outer", (string?)error["transpilerException"]?["message"]);
        Assert.Equal("inner", (string?)error["transpilerException"]?["inner"]?["message"]);
        Assert.All(
            report.Properties(),
            property => Assert.True(char.IsLower(property.Name[0]), property.Name));

        log.Begin(directory.Path, continueOnError: false, buildCommand: "build");
        log.RecordSuccess(succeeded);
        Assert.Null(log.Finish());
        Assert.False(File.Exists(reportPath));
    }

    private static ProjectTranspiler Step(string name) =>
        new()
        {
            Name = name,
            RelativePath = $"/{name.ToLowerInvariant()}",
            CommandLine = name.ToLowerInvariant(),
        };
}
