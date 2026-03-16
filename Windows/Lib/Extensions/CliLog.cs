using System;

namespace SSoTme.OST.Lib.Extensions
{
    public static class CliLog
    {
        public static void Writing(string path) => LogLine("Creating", path, ConsoleColor.Green);
        public static void Cleaning(string path) => LogLine("Cleaning", path, ConsoleColor.Red);

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
    }
}
