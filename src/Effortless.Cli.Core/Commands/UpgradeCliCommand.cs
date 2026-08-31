using Effortless.Cli.Updates;

namespace Effortless.Cli.Commands;

public sealed class UpgradeCliCommand
{
    private readonly CliUpdater _updater;

    public UpgradeCliCommand(CliUpdater updater = null)
    {
        _updater = updater ?? new CliUpdater();
    }

    public int Run() => _updater.Run();
}
