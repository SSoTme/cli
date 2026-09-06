using System.Diagnostics;
using System.Reflection;

namespace Effortless.Cli.Project;

public static class ChildProcessRunner
{
    public const string ChildProcessEnvVariable = "EFFORTLESS_CHILD_PROCESS";

    public static void InvokeEffortlessBuild(this DirectoryInfo directory)
    {
        if (!directory.Exists)
        {
            throw new NotImplementedException(
                "The specified directory does not exist.");
        }

        Console.WriteLine(
            $"Executing 'effortless -buildLocal' in {directory.FullName}");
        InvokeCurrent(
            directory,
            "-buildLocal");
    }

    public static void InvokeEffortlessDescribe(this DirectoryInfo directory)
    {
        if (!directory.Exists)
        {
            throw new NotImplementedException(
                "The specified directory does not exist.");
        }

        Console.WriteLine(
            $"Executing 'effortless -describeAll' in {directory.FullName}");
        InvokeCurrent(
            directory,
            "-describeAll");
    }

    public static void InvokeEffortlessClean(this DirectoryInfo directory)
    {
        if (!directory.Exists)
        {
            throw new NotImplementedException(
                "The specified directory does not exist.");
        }

        Console.WriteLine(
            $"Executing 'effortless -clean' in {directory.FullName}");
        InvokeCurrent(
            directory,
            "-clean");
    }

    internal static void InvokeCurrent(
        DirectoryInfo directory,
        params string[] arguments)
    {
        var processPath = Environment.ProcessPath;
        if (String.IsNullOrEmpty(processPath))
        {
            throw new InvalidOperationException(
                "Cannot start a child CLI process because Environment.ProcessPath is unavailable.");
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = processPath,
            WorkingDirectory = directory.FullName,
            UseShellExecute = false
        };

        if (String.Equals(
                Path.GetFileNameWithoutExtension(processPath),
                "dotnet",
                StringComparison.OrdinalIgnoreCase))
        {
            var currentDll = Assembly.GetEntryAssembly()?.Location;
            if (String.IsNullOrEmpty(currentDll))
            {
                throw new InvalidOperationException(
                    "Cannot start a child CLI process because the current DLL path is unavailable.");
            }

            processStartInfo.ArgumentList.Add(currentDll);
        }

        foreach (var argument in arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        Environment.SetEnvironmentVariable(
            ChildProcessEnvVariable,
            "1");
        try
        {
            var process = Process.Start(processStartInfo);
            if (process == null)
            {
                throw new InvalidOperationException(
                    $"Failed to start child CLI process '{processPath}'.");
            }

            process.WaitForExit(300000);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ChildProcessEnvVariable,
                null);
        }
    }

    public static bool IsIgnored(this DirectoryInfo subDirToCheck)
    {
        if (subDirToCheck.Name == ".git")
        {
            return true;
        }

        if (subDirToCheck.Name == EffortlessProject.LedgerDirectoryName
            || subDirToCheck.Name == EffortlessProject.LegacyLedgerDirectoryName)
        {
            return true;
        }

        if (subDirToCheck.Name == ".vs")
        {
            return true;
        }

        return false;
    }
}
