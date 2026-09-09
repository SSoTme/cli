#nullable enable
using Effortless.Cli.Options;
using Effortless.Cli.Project;

namespace Effortless.Cli.Commands;

public sealed class BuildCommand
{
    private readonly Func<string, EffortlessProject, bool, BuildErrorLog, int>
        _runCommandLine;
    private readonly TriggerBuildWatcher _triggerWatcher;

    public BuildCommand(
        Func<string, EffortlessProject, bool, BuildErrorLog, int> runCommandLine,
        TriggerBuildWatcher? triggerWatcher = null)
    {
        _runCommandLine = runCommandLine;
        _triggerWatcher =
            triggerWatcher ?? new TriggerBuildWatcher();
    }

    public int Run(
        CliInvocation invocation,
        bool all,
        bool withSubprojects = false)
    {
        if (string.IsNullOrWhiteSpace(
                invocation.Options.buildOnTrigger))
        {
            return RunOnce(invocation, all, withSubprojects);
        }

        _triggerWatcher.WatchAsync(
                invocation.Options.buildOnTrigger,
                () =>
                {
                    var result = RunOnce(
                        invocation,
                        all,
                        withSubprojects);
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
        bool all,
        bool withSubprojects)
    {
        var project = invocation.Project!;
        var command = withSubprojects
            ? "buildWithSubprojects"
            : all
                ? "buildAll"
                : invocation.Options.buildLocal
                    ? "build -buildLocal"
                    : "build";
        invocation.BuildErrorLog.Begin(
            project.RootPath,
            invocation.Options.continueOnError,
            command);
        try
        {
            // A build that matches no steps otherwise prints nothing at all and
            // exits zero, which reads as a broken CLI rather than an empty
            // project. `-init` sets build itself, so only say this when the user
            // actually asked for a build.
            if (!invocation.Options.init
                && (project.ProjectTranspilers is null
                    || project.ProjectTranspilers.Count == 0))
            {
                CliLog.LogLine(
                    "Nothing to build: no transpiler steps are registered in "
                    + "effortless.json.",
                    ConsoleColor.Yellow);
                CliLog.LogLine(
                    "Add one with: effortless -install <tool-name>",
                    ConsoleColor.Yellow);
            }

            var runner = new BuildRunner(
                project,
                _runCommandLine,
                invocation.BuildErrorLog);
            if (all)
            {
                runner.RebuildAll(
                    project.RootPath,
                    invocation.Options.includeDisabled,
                    invocation.Options.transpilerGroup,
                    invocation.Options.buildLocal,
                    invocation.Options.debug,
                    invocation.Options.continueOnError,
                    withSubprojects);
            }
            else
            {
                runner.Rebuild(
                    invocation.CurrentDirectory!,
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
            invocation.BuildErrorLog.Finish();
        }
    }
}
