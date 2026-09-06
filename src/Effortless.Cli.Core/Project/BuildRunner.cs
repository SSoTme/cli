#nullable enable
using Effortless.Cli;

namespace Effortless.Cli.Project;

public class BuildRunner
{
    private readonly EffortlessProject _project;
    private readonly Func<string, EffortlessProject, bool, BuildErrorLog, int>
        _runCommandLine;
    private readonly BuildErrorLog _buildErrorLog;
    private List<string> _projectFiles = new List<string>();

    public BuildRunner(
        EffortlessProject project,
        Func<string, EffortlessProject, bool, BuildErrorLog, int> runCommandLine,
        BuildErrorLog buildErrorLog)
    {
        _project = project;
        _runCommandLine = runCommandLine;
        _buildErrorLog = buildErrorLog;
    }

    public void RebuildAll(
        string rootPath,
        bool includeDisabled,
        string transpilerGroup,
        bool isLocalBuild,
        bool debug,
        bool continueOnError = false,
        bool withSubprojects = false)
    {
        Rebuild(
            rootPath,
            includeDisabled,
            transpilerGroup,
            isLocalBuild,
            debug,
            true,
            continueOnError,
            withSubprojects);
    }

    public void Rebuild(
        string buildPath,
        bool includeDisabled,
        string transpilerGroup,
        bool isBuildLocal,
        bool debug,
        bool isBuildAll = false,
        bool continueOnError = false,
        bool withSubprojects = false)
    {
        DoRebuild(
            buildPath,
            includeDisabled,
            transpilerGroup,
            isBuildLocal,
            debug,
            isBuildAll,
            continueOnError,
            withSubprojects);
    }

    internal void DoRebuild(
        string buildPath,
        bool includeDisabled,
        string transpilerGroup,
        bool isBuildLocal,
        bool debugOption,
        bool isBuildAll = false,
        bool continueOnError = false,
        bool withSubprojects = false)
    {
        if (!isBuildLocal)
        {
            CheckIfParentIsRootSeed();
        }

        // D6/D12: buildAll means "as if run from the project root". Nested
        // effortless projects are normally excluded; only buildWithSubprojects
        // walks into them.
        if (withSubprojects)
        {
            _projectFiles = NestedProjectFinder.Find(_project.RootPath)
                .Select(directory => directory.FullName)
                .ToList();
        }

        var currentDirectory = Environment.CurrentDirectory;
        try
        {
            var relativePath =
                _project.GetProjectRelativePath(buildPath);
            var matchingProjectTranspilers =
                (_project.ProjectTranspilers?.Where(
                     projectTranspiler =>
                         projectTranspiler.IsAtPath(
                             relativePath,
                             exactMatch: isBuildLocal)) ??
                 new List<ProjectTranspiler>())
                .Where(projectTranspiler =>
                    String.IsNullOrEmpty(transpilerGroup) ||
                    String.Equals(
                        projectTranspiler.TranspilerGroup,
                        transpilerGroup,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingProjectTranspilers.Any(
                    projectTranspiler =>
                        projectTranspiler.SyncCommandLineVersion()))
            {
                _project.Save();
            }

            foreach (var projectTranspiler in
                     matchingProjectTranspilers)
            {
                if (!projectTranspiler.IsDisabled ||
                    includeDisabled)
                {
                    try
                    {
                        Rebuild(
                            projectTranspiler,
                            debugOption,
                            continueOnError);
                    }
                    catch (TranspilerStepFailedException ex)
                    {
                        if (!continueOnError)
                        {
                            throw;
                        }

                        CliLog.LogLine(
                            $"{ex.Message}",
                            ConsoleColor.Red);
                        CliLog.LogLine(
                            "-continueOnError is set — moving on to the next step.",
                            ConsoleColor.Yellow);
                    }
                    catch (Exception ex)
                    {
                        if (!continueOnError)
                        {
                            throw;
                        }

                        _buildErrorLog.RecordFailure(
                            projectTranspiler,
                            -1,
                            null,
                            ex,
                            null,
                            null);
                        CliLog.LogLine(
                            $"Transpiler '{projectTranspiler.Name}' threw: {ex.Message}",
                            ConsoleColor.Red);
                        CliLog.LogLine(
                            "-continueOnError is set — moving on to the next step.",
                            ConsoleColor.Yellow);
                    }
                }
                else
                {
                    _buildErrorLog.RecordSkipped(
                        projectTranspiler,
                        "IsDisabled is true in effortless.json");
                    Console.WriteLine(
                        "\n\n - SKIPPING DISABLED TRANSPILER: {0}\n - {1}\n - {2}\n\n",
                        projectTranspiler.Name,
                        projectTranspiler.RelativePath,
                        projectTranspiler.CommandLine);
                }
            }

            if (withSubprojects)
            {
                BuildSubProjects();
            }

            new EmptyFolderPruner(_project)
                .RemoveEmptyFolders(buildPath);
        }
        finally
        {
            Environment.CurrentDirectory = currentDirectory;
        }
    }

    private void Rebuild(
        ProjectTranspiler projectTranspiler,
        bool debugOption,
        bool continueOnError = false)
    {
        var commandLineToRun = projectTranspiler.CommandLine;
        if (debugOption)
        {
            commandLineToRun += " -debug";
        }

        Console.WriteLine(
            "\n\n **** " +
            projectTranspiler.RelativePath +
            ": " +
            projectTranspiler.Name +
            " ****");
        Console.WriteLine(
            "CommandLine:> effortless {0}",
            commandLineToRun);

        var transpileRootDirectory = new DirectoryInfo(
            Path.Combine(
                _project.RootPath,
                $"{projectTranspiler.RelativePath}".Trim(
                    "\\/".ToCharArray())));
        if (!transpileRootDirectory.Exists)
        {
            transpileRootDirectory.Create();
        }

        var originalDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory =
                transpileRootDirectory.FullName;

            var cliResult = _runCommandLine(
                commandLineToRun,
                _project,
                continueOnError,
                _buildErrorLog);
            if (cliResult != 0)
            {
                _buildErrorLog.RecordFailure(
                    projectTranspiler,
                    cliResult,
                    null,
                    null,
                    null,
                    null);

                var errorMessage =
                    $"exited with code {cliResult} but reported no error message";
                throw new TranspilerStepFailedException(
                    $"Transpiler '{projectTranspiler.Name}' failed: {errorMessage}");
            }

            _buildErrorLog.RecordSuccess(projectTranspiler);
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
        }
    }

    private void CheckIfParentIsRootSeed()
    {
        if (File.Exists("../effortless.json") ||
            File.Exists("../ssotme.json"))
        {
            ChildProcessRunner.InvokeCurrent(
                new DirectoryInfo(".."),
                "-buildLocal",
                "-tg",
                "ssot");
        }
    }

    private void BuildSubProjects()
    {
        foreach (var projectDirectory in _projectFiles)
        {
            new DirectoryInfo(projectDirectory).InvokeEffortlessBuild();
        }
    }
}
