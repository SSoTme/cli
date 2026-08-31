using Effortless.Cli.Options;
using Effortless.Cli.Project;

namespace Effortless.Cli.Commands;

public sealed class BuildCommand
{
    private readonly Func<string, EffortlessProject, bool, int>
        _runCommandLine;
    private readonly TriggerBuildWatcher _triggerWatcher;

    public BuildCommand(
        Func<string, EffortlessProject, bool, int> runCommandLine,
        TriggerBuildWatcher triggerWatcher = null)
    {
        _runCommandLine = runCommandLine;
        _triggerWatcher =
            triggerWatcher ?? new TriggerBuildWatcher();
    }

    public int Run(CliInvocation invocation, bool all)
    {
        if (string.IsNullOrWhiteSpace(
                invocation.Options.buildOnTrigger))
        {
            return RunOnce(invocation, all);
        }

        _triggerWatcher.WatchAsync(
                invocation.Options.buildOnTrigger,
                () =>
                {
                    var result = RunOnce(invocation, all);
                    if (result != 0)
                    {
                        throw new InvalidOperationException(
                            $"Triggered build exited with code {result}.");
                    }

                    return Task.CompletedTask;
                })
            .GetAwaiter()
            .GetResult();
        return 0;
    }

    private int RunOnce(
        CliInvocation invocation,
        bool all)
    {
        var project = invocation.Project;
        var command = all
            ? "buildAll"
            : invocation.Options.buildLocal
                ? "build -buildLocal"
                : "build";
        BuildErrorLog.Begin(
            project.RootPath,
            invocation.Options.continueOnError,
            command);
        try
        {
            var runner = new BuildRunner(project, _runCommandLine);
            if (all)
            {
                runner.RebuildAll(
                    project.RootPath,
                    invocation.Options.includeDisabled,
                    invocation.Options.transpilerGroup,
                    invocation.Options.buildLocal,
                    invocation.Options.debug,
                    invocation.Options.continueOnError);
            }
            else
            {
                runner.Rebuild(
                    invocation.CurrentDirectory,
                    invocation.Options.includeDisabled,
                    invocation.Options.transpilerGroup,
                    invocation.Options.buildLocal,
                    invocation.Options.debug,
                    continueOnError:
                    invocation.Options.continueOnError);
            }

            return 0;
        }
        finally
        {
            BuildErrorLog.Finish();
        }
    }
}
