using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

public static class RepositoryManager
{

    public static string CloneRepositoryUsingCmd(this string seedRepoUrl, string directoryName)
    {
        try
        {
            directoryName = directoryName ?? Path.GetFileNameWithoutExtension(seedRepoUrl);

            // Setting up the process start information
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"clone {seedRepoUrl} \"{directoryName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Starting the process
            using (var process = new Process { StartInfo = processStartInfo })
            {
                process.Start();

                // Reading output to console
                string output = process.StandardOutput.ReadToEnd();
                string errors = process.StandardError.ReadToEnd();

                process.WaitForExit();

                // Handling results based on process exit code
                if (process.ExitCode == 0)
                {
                    Console.WriteLine("Repository successfully cloned.");
                    Console.WriteLine(output);
                }
                else
                {
                    Console.WriteLine("Failed to clone the repository:");
                    Console.WriteLine(errors);
                    throw new Exception($"Failed to clone the respository: \n{errors}");
                }
            }
            return new FileInfo(directoryName).FullName;
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }
}
