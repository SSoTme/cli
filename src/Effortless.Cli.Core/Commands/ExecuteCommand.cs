using System.ComponentModel;
using System.Diagnostics;
using Effortless.Cli.FileSets;
using Effortless.Cli.Options;
using Effortless.Cli.Project;

namespace Effortless.Cli.Commands;

public sealed class ExecuteCommand
{
    public int Run(CliInvocation invocation)
    {
        if (!invocation.Options.install)
        {
            var commandLine = invocation.Options.execute + " ";
            var separator = commandLine.IndexOf(' ');
            var executable = commandLine[..separator];
            var arguments = commandLine[(separator + 1)..];

            try
            {
                using var process = Process.Start(
                    new ProcessStartInfo(executable, arguments));
                process.WaitForExit(invocation.Options.waitTimeout);
                if (!process.HasExited)
                {
                    process.Kill();
                    throw new NoStackException(
                        $"Timed out waiting for process to complete: {invocation.Options.execute}");
                }
            }
            catch (Win32Exception)
            {
                throw new NoStackException(
                    $"The system couldn't find the command \"{executable}\"");
            }
        }

        if (invocation.Options.install)
        {
            var payload = new TranspilePayload
            {
                Transpiler = new Transpiler
                {
                    Name = "-execute",
                },
            };
            invocation.Project.Install(
                payload,
                invocation.Options.transpilerGroup,
                invocation.ResolvedVersionKey);
        }

        return 0;
    }
}
