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

            EnsureExecutable(executable);

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
                pinnedVersion: null);
        }

        return 0;
    }

    /// <summary>
    /// A rulebook-generated script can lose its executable bit on every build
    /// (FileSetCleaner deletes-then-recreates an unmodified FileSet entry so it
    /// can follow a relative-path move; the fresh file carries default, non-
    /// executable permissions). -execute targets are the one place the CLI
    /// itself runs such a file directly, so it restores the bit here rather
    /// than relying on the file having kept it.
    /// </summary>
    private static void EnsureExecutable(string executable)
    {
        if (OperatingSystem.IsWindows() || !File.Exists(executable))
        {
            return;
        }

        var mode = File.GetUnixFileMode(executable);
        const UnixFileMode executeBits =
            UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        if ((mode & UnixFileMode.UserExecute) == 0)
        {
            File.SetUnixFileMode(executable, mode | executeBits);
        }
    }
}
