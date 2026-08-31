namespace Effortless.Cli;

public static class ToolLogPrinter
{
    public static void DisplayLogEntry(
        LogEntry log,
        bool showDebug,
        string transpilerName = null)
    {
        if (log == null || string.IsNullOrEmpty(log.Text))
        {
            return;
        }

        if ((log.Level ?? "message").ToLower() == "debug" &&
            !showDebug)
        {
            return;
        }

        var currentColor = Console.ForegroundColor;

        switch ((log.Level ?? "message").ToLower())
        {
            case "error":
                Console.ForegroundColor = ConsoleColor.Red;
                break;
            case "warning":
                Console.ForegroundColor = ConsoleColor.Yellow;
                break;
            case "info":
                Console.ForegroundColor = ConsoleColor.Cyan;
                break;
            case "debug":
                Console.ForegroundColor = ConsoleColor.DarkGray;
                break;
            case "message":
            default:
                Console.ForegroundColor = ConsoleColor.Gray;
                break;
        }

        var logText = log.Text;
        if (!string.IsNullOrEmpty(transpilerName))
        {
            logText = $"[{transpilerName}] {logText}";
        }

        Console.WriteLine(logText);
        Console.ForegroundColor = currentColor;
    }
}
