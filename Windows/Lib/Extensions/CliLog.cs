using System;

namespace SSoTme.OST.Lib.Extensions
{
    public static class CliLog
    {
        [ThreadStatic]
        private static bool _suppressFileLog;
        public static bool SuppressFileLog
        {
            get => _suppressFileLog;
            set => _suppressFileLog = value;
        }

        public static void Writing(string path) { if (!SuppressFileLog) LogLine("Creating", path, ConsoleColor.Green); }
        public static void Cleaning(string path) { if (!SuppressFileLog) LogLine("Cleaning", path, ConsoleColor.Red); }

        static private ConsoleColor CliLogIdColor = ConsoleColor.Blue;

        public static void LogLine(string action, string message, ConsoleColor actionColor)
        {
            Console.ForegroundColor = CliLogIdColor;
            Console.Write("[cli] ");
            Console.ForegroundColor = actionColor;
            Console.Write($"{action} ");
            Console.ResetColor();
            Console.WriteLine($"{message}");
        }

        public static void LogLine(string message, ConsoleColor actionColor)
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

        public static void LogTranspiler(string action, ConsoleColor actionColor, string commandLine, string relativePath, string transpilerGroup)
        {
            Console.ForegroundColor = CliLogIdColor;
            Console.Write("[cli] ");
            Console.ForegroundColor = actionColor;
            Console.WriteLine(action);
            Console.ResetColor();
            Console.WriteLine($"      CommandLine: {commandLine}");
            Console.WriteLine($"      TranspilerGroup: {transpilerGroup ?? "(none)"}");
            Console.WriteLine($"      RelativePath: {(String.IsNullOrEmpty(relativePath) ? "(project root)" : relativePath)}");
        }
    }
}
