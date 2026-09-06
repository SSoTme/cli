#nullable enable
namespace Effortless.Cli;

public static class CliLog
{
    private static readonly AsyncLocal<bool> _suppressFileLog = new();

    public static bool SuppressFileLog
    {
        get => _suppressFileLog.Value;
        set => _suppressFileLog.Value = value;
    }

    public static void Writing(string path)
    {
        if (!SuppressFileLog)
        {
            LogLine("Creating", path, ConsoleColor.Green);
        }
    }

    public static void Cleaning(string path)
    {
        if (!SuppressFileLog)
        {
            LogLine("Cleaning", path, ConsoleColor.Red);
        }
    }

    private static ConsoleColor CliLogIdColor = ConsoleColor.Blue;

    public static void LogLine(
        string action,
        string message,
        ConsoleColor actionColor)
    {
        Console.ForegroundColor = CliLogIdColor;
        Console.Write("[cli] ");
        Console.ForegroundColor = actionColor;
        Console.Write($"{action} ");
        Console.ResetColor();
        Console.WriteLine($"{message}");
    }

    public static void LogLine(
        string message,
        ConsoleColor actionColor)
    {
        Console.ForegroundColor = CliLogIdColor;
        Console.Write("[cli] ");
        Console.ForegroundColor = actionColor;
        Console.WriteLine($"{message}");
        Console.ResetColor();
    }

    public static void LogLine(string message)
    {
        Console.ForegroundColor = CliLogIdColor;
        Console.Write("[cli] ");
        Console.ResetColor();
        Console.WriteLine($"{message}");
    }

    public static void LogTranspiler(
        string action,
        ConsoleColor actionColor,
        string commandLine,
        string relativePath,
        string? transpilerGroup)
    {
        Console.ForegroundColor = CliLogIdColor;
        Console.Write("[cli] ");
        Console.ForegroundColor = actionColor;
        Console.WriteLine(action);
        Console.ResetColor();
        Console.WriteLine($"      CommandLine: {commandLine}");
        Console.WriteLine(
            $"      TranspilerGroup: {transpilerGroup ?? "(none)"}");
        Console.WriteLine(
            $"      RelativePath: {(String.IsNullOrEmpty(relativePath) ? "(project root)" : relativePath)}");
    }
}
