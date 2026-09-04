using Effortless.Cli.Project;

namespace Effortless.Cli;

public sealed class CloudBridgeClient
{
    private readonly Func<string, EffortlessProject, bool, int>
        _runCommandLine;

    public CloudBridgeClient(
        Func<string, EffortlessProject, bool, int> runCommandLine)
    {
        _runCommandLine = runCommandLine
            ?? throw new ArgumentNullException(nameof(runCommandLine));
    }

    public bool Refresh(RemoteToolsRefreshRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Run(
            request.CommandLine,
            request.WorkingDirectory,
            request.IndexFile.Name,
            out _);
    }

    private bool Run(
        string commandLine,
        string workingDirectory,
        string outputFileName,
        out string output)
    {
        output = null;
        Directory.CreateDirectory(workingDirectory);
        var projectFile = ProjectLocator.GetProjectFileAt(
            new DirectoryInfo(workingDirectory),
            reverseUpdate: false);
        if (!projectFile.Exists)
        {
            var project = new EffortlessProject
            {
                Name = "remote_tools",
                RootPath = workingDirectory,
            };
            project.ProjectSettings.Add(
                new ProjectSetting
                {
                    Name = "project-name",
                    Value = "remote_tools",
                });
            project.Save(new DirectoryInfo(workingDirectory));
            projectFile.Refresh();
        }

        var loadedProject = ProjectLocator.Load(
            projectFile,
            new DirectoryInfo(workingDirectory));
        var savedDirectory = Environment.CurrentDirectory;
        var savedSuppress = CliLog.SuppressFileLog;
        try
        {
            Environment.CurrentDirectory = workingDirectory;
            CliLog.SuppressFileLog = true;
            var result = _runCommandLine(
                commandLine,
                loadedProject,
                false);
            if (result != 0)
            {
                return false;
            }

            var outputPath = Path.Combine(
                workingDirectory,
                outputFileName);
            if (!File.Exists(outputPath))
            {
                return false;
            }

            output = File.ReadAllText(outputPath);
            return true;
        }
        finally
        {
            CliLog.SuppressFileLog = savedSuppress;
            if (Directory.Exists(savedDirectory))
            {
                Environment.CurrentDirectory = savedDirectory;
            }
        }
    }
}
