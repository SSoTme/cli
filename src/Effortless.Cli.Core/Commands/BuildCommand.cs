using Effortless.Cli.Options;
using Effortless.Cli.Project;

namespace Effortless.Cli.Commands;

public sealed class BuildCommand
{
    private readonly Func<string, EffortlessProject, bool, int>
        _runCommandLine;

    public BuildCommand(
        Func<string, EffortlessProject, bool, int> runCommandLine)
    {
        _runCommandLine = runCommandLine;
    }

    public int Run(CliInvocation invocation, bool all)
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
