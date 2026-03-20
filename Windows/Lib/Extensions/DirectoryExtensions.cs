using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

public static class DirectoryExtensions
{
    public static void StartSeedBuild(this DirectoryInfo seedDI)
    {
        string seedFilePath = Path.Combine(seedDI.FullName, "ssotme-seed.json");

        // Check if the file exists
        if (!File.Exists(seedFilePath))
        {
            Console.WriteLine($"File ssotme-seed.json not found in {seedDI.FullName}");
            return;
        }

        // Read and parse the JSON file
        var jsonContent = File.ReadAllText(seedFilePath);
        var jsonObject = JObject.Parse(jsonContent);

        // Extract "start-on-complete" if it exists
        var startOnCompleteJO = jsonObject["start-on-complete"];
        var startOnCompleteCommands = startOnCompleteJO?.ToObject<List<string>>();

        if (startOnCompleteCommands != null && startOnCompleteCommands.Any())
        {
            Console.WriteLine($"Starting processes defined in 'start-on-complete'...");


            foreach (var command in startOnCompleteCommands)
            {
                // Start each command using Process.Start
                Console.WriteLine($"Executing: {command}");
                bool isLast = startOnCompleteCommands.IndexOf(command) == startOnCompleteCommands.Count - 1;
                StartProcess(command, seedDI.Name, !isLast);
            }
        }
        else
        {
            Console.WriteLine($"No commands found in 'start-on-complete'.");
        }
    }

    // Method to start a process and not wait for it to finish
    private static void StartProcess(string command, string seedPath, bool wait)
    {
        try
        {
            // Split the command and its arguments if any
            var parts = command.Split(' ', 2);
            var processStartInfo = new ProcessStartInfo
            {
                FileName = parts[0],   // e.g., 'npm', 'msbuild', 'go'
                Arguments = parts.Length > 1 ? parts[1] : "",  // e.g., 'install', 'run start', 'main'
                UseShellExecute = true,  // This allows running external commands like npm
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                WorkingDirectory = seedPath
            };

            var process = Process.Start(processStartInfo);
            if (wait) process.WaitForExit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start process: {command}. Error: {ex.Message}");
        }
    }

    public static void InvokeSSoTmeBuild(this DirectoryInfo di)
    {
        if (!di.Exists)
        {
            throw new NotImplementedException("The specified directory does not exist.");
        }

        Console.WriteLine($"Executing 'effortless -buildLocal' in {di.FullName}");
        ProcessStartInfo psi;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi = new ProcessStartInfo("cmd.exe", $"/c effortless -buildLocal") { WorkingDirectory = di.FullName };
        }
        else
        {
            psi = new ProcessStartInfo("/bin/bash", $"-c \"effortless -buildLocal\"") { WorkingDirectory = di.FullName };
        }
        Environment.SetEnvironmentVariable("SSOTME_CHILD_PROCESS", "1");
        var p = Process.Start(psi);
        p.WaitForExit(300000);
        Environment.SetEnvironmentVariable("SSOTME_CHILD_PROCESS", null);
    }

    public static void CheckSSoTmeCache(this DirectoryInfo seedDI, string fullSeedName)
    {
        string userSeedCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssotme", "seed_cache", fullSeedName, "cache");

        // Check if the cache directory exists
        if (!Directory.Exists(userSeedCachePath))
        {
            Console.WriteLine("Cache directory does not exist.");
            return;
        }

        DirectoryInfo cacheDir = new DirectoryInfo(userSeedCachePath);

        // Move files from cache to seed directory
        foreach (FileInfo file in cacheDir.GetFiles())
        {
            string destFilePath = Path.Combine(seedDI.FullName, file.Name);
            if (File.Exists(destFilePath)) File.Delete(destFilePath);
            file.MoveTo(destFilePath);
        }

        // Move directories from cache to seed directory
        foreach (DirectoryInfo dir in cacheDir.GetDirectories())
        {
            string destDirPath = Path.Combine(seedDI.FullName, dir.Name);
            //if (Directory.Exists(destDirPath)) Directory.Delete(destDirPath, true);            
            if (!Directory.Exists(destDirPath)) Directory.Move(dir.FullName, destDirPath);
            else
            {
                CopyDirectoryContents(dir.FullName, destDirPath);
            }
        }

        // Process ZIP files
        foreach (FileInfo zipFile in cacheDir.GetFiles("*.zip"))
        {
            // Starting a new process to handle the ZIP extraction
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"Expand-Archive -Path '{zipFile.FullName}' -DestinationPath '{seedDI.FullName}' -Force\"",
                UseShellExecute = true, // Change to true to allow the window to be shown if needed
                CreateNoWindow = false // Change to false to allow seeing the window
            };

            // Start the process without waiting for it to exit
            Process proc = Process.Start(startInfo);
        }
    }


    // Function to copy directory contents recursively
    private static void CopyDirectoryContents(string sourceDir, string destinationDir)
    {
        // Ensure the destination directory exists
        Directory.CreateDirectory(destinationDir);

        // Copy all the files in the current directory
        foreach (string filePath in Directory.GetFiles(sourceDir))
        {
            string fileName = Path.GetFileName(filePath);
            string destFilePath = Path.Combine(destinationDir, fileName);

            // Copy file only if it doesn't already exist
            if (!File.Exists(destFilePath))
            {
                File.Copy(filePath, destFilePath);
            }
        }

        // Recursively copy all subdirectories
        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            string dirName = Path.GetFileName(subDir);
            string destSubDirPath = Path.Combine(destinationDir, dirName);

            // Recursively copy contents of subdirectory
            CopyDirectoryContents(subDir, destSubDirPath);
        }
    }

    public static void InvokeSSoTmeClean(this DirectoryInfo di)
    {
        if (!di.Exists)
        {
            throw new NotImplementedException("The specified directory does not exist.");
        }

        Console.WriteLine($"Executing 'effortless -clean' in {di.FullName}");
        ProcessStartInfo psi;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi = new ProcessStartInfo("cmd.exe", $"/c effortless -clean") { WorkingDirectory = di.FullName };
        }
        else
        {
            psi = new ProcessStartInfo("/bin/bash", $"-c \"effortless -clean\"") { WorkingDirectory = di.FullName };
        }
        Environment.SetEnvironmentVariable("SSOTME_CHILD_PROCESS", "1");
        var p = Process.Start(psi);
        p.WaitForExit(300000);
        Environment.SetEnvironmentVariable("SSOTME_CHILD_PROCESS", null);
    }




    public static bool IsIgnored(this DirectoryInfo subDirToCheck)
    {
        if (subDirToCheck.Name == ".git") return true;
        if (subDirToCheck.Name == ".ssotme") return true;
        if (subDirToCheck.Name == ".vs") return true;

        return false;
    }
}
