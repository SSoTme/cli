using Effortless.Cli.Commands;
using Effortless.Cli.FileSets;
using Effortless.Cli.Project;

namespace Effortless.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
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

    private static void ShowError(string message)
    {
        var color = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ForegroundColor = color;
    }
}
