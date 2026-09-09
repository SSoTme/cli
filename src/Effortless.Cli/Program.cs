using Effortless.Cli.Commands;
using Effortless.Cli.Config;
using Effortless.Cli.FileSets;
using Effortless.Cli.Project;
using Effortless.Cli.Updates;

namespace Effortless.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            UserConfigMigration.EnsureMigrated();
            MaybeShowPendingUpdateNotice();
            MaybeStartBackgroundUpdateCheck();
            return new CommandDispatcher().Run(args);
        }
        catch (NoStackException exception)
        {
            ShowError(exception.Message);
            return -1;
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
            return -1;
        }
    }

    /// <summary>
    /// A prior background reinstall (see <see cref="MaybeStartBackgroundUpdateCheck"/>)
    /// leaves a pending notice; the next command prints it once, before its
    /// own output, then clears it. Suppressed for child-process invocations
    /// (build-loop / bridge sub-invocations) so it is never printed twice for
    /// one user-initiated command.
    /// </summary>
    private static void MaybeShowPendingUpdateNotice()
    {
        if (IsChildProcess())
        {
            return;
        }

        var notice = UpdateCheckState.TakePendingNotice();
        if (!string.IsNullOrEmpty(notice))
        {
            Console.WriteLine(notice);
        }
    }

    /// <summary>
    /// Fires the once-daily update check without ever blocking, delaying, or
    /// being awaited by the foreground command: Task.Run and forget. Only the
    /// true one-per-process entry point calls this — never
    /// CommandDispatcher.RunInvocation, which recurses for build-loop/bridge
    /// sub-invocations and must not re-trigger it (guarded here via
    /// EFFORTLESS_CHILD_PROCESS, the same flag those sub-invocations set).
    /// </summary>
    private static void MaybeStartBackgroundUpdateCheck()
    {
        if (IsChildProcess())
        {
            return;
        }

        if (UpdateCheckState.GetLastCheckedDate() == TodayUtc())
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                // Gate first, inside the task: a race between two
                // near-simultaneous processes is low-stakes and not worth
                // synchronizing beyond check-then-set.
                UpdateCheckState.SetLastCheckedDate(TodayUtc());
                await new UpdateChecker().RunBackgroundAsync();
            }
            catch
            {
                // Never let the background check surface anything to the
                // foreground command.
            }
        });
    }

    private static bool IsChildProcess() =>
        !string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable(
                Effortless.Cli.Project.ChildProcessRunner.ChildProcessEnvVariable));

    private static DateOnly TodayUtc()
    {
        var raw = Environment.GetEnvironmentVariable("EFFORTLESS_CLI_TEST_UTC_NOW");
        return DateTimeOffset.TryParse(
            raw,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal
            | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var value)
            ? DateOnly.FromDateTime(value.UtcDateTime)
            : DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static void ShowError(string message)
    {
        var color = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ForegroundColor = color;
    }
}
