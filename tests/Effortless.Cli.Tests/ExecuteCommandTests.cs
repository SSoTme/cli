using Effortless.Cli.Commands;
using Effortless.Cli.Options;

namespace Effortless.Cli.Tests;

public class ExecuteCommandTests
{
    [Fact(DisplayName = "unit-execute-restores-exec-bit: -execute chmods a non-executable script before running it")]
    public void ExecuteRestoresExecuteBitBeforeRunning()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TestDirectory();
        var scriptPath = directory.File("regenerated.sh");
        File.WriteAllText(scriptPath, "#!/bin/sh\nexit 0\n");
        File.SetUnixFileMode(
            scriptPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        var invocation = new CliInvocation
        {
            Options = new CliOptions
            {
                execute = scriptPath,
                waitTimeout = 5000,
            },
        };

        var exitCode = new ExecuteCommand().Run(invocation);

        Assert.Equal(0, exitCode);
        Assert.NotEqual(
            UnixFileMode.None,
            File.GetUnixFileMode(scriptPath) & UnixFileMode.UserExecute);
    }
}
