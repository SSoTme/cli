using Effortless.Cli.Commands;
using Effortless.Cli.Project;

namespace Effortless.Cli.Tests;

public sealed class CommandDispatcherTests
{
    [Fact(DisplayName = "unit-bridge-isolation: bridge parsing skips remote lookup and version labels")]
    public void BridgeParsingUsesAnIsolatedInvocation()
    {
        var dispatcher = new CommandDispatcher();
        var project = new EffortlessProject
        {
            Name = "remote_tools",
            RootPath = Environment.CurrentDirectory,
        };

        var invocation = dispatcher.ParseBridgeInvocation(
            "https://bridge.invalid/run -p action=refresh",
            project,
            continueOnError: true);
        var buildInvocation = dispatcher.ParseBuildInvocation(
            "https://tool.invalid/run -p action=build",
            project,
            continueOnError: false);

        Assert.Same(project, invocation.Project);
        Assert.True(invocation.IsBuildOperation);
        Assert.True(invocation.ContinueOnError);
        Assert.True(invocation.Options.continueOnError);
        Assert.True(invocation.SkipRemoteToolsLookup);
        Assert.True(invocation.SuppressVersionLabel);
        Assert.Equal(Environment.CurrentDirectory, invocation.CurrentDirectory);
        Assert.False(buildInvocation.SkipRemoteToolsLookup);
        Assert.False(buildInvocation.SuppressVersionLabel);
        Assert.False(buildInvocation.ContinueOnError);
    }
}
