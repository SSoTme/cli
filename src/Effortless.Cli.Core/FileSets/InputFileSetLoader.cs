using Effortless.Cli.Project;

namespace Effortless.Cli.FileSets;

public class InputFileSetLoader
{
    private readonly List<string> _input;
    private readonly EffortlessProject _project;

    public InputFileSetLoader(
        List<string> input,
        EffortlessProject project)
    {
        _input = input;
        _project = project;
        OptionalInputCLIInputs = new List<string>();
    }

    public List<string> OptionalInputCLIInputs { get; internal set; }

    public string InputFileContents { get; private set; }

    public string InputFileSetXml { get; private set; }

    public FileSet FileSet { get; private set; }

    private void SaveOptionalCLIInputs()
    {
        OptionalInputCLIInputs =
            OptionalInputCLIInputs ?? new List<string>();
        var inputFileCount = _input is null ? 0 : _input.Count();
        for (int fileIndex = 0; fileIndex < inputFileCount; fileIndex++)
        {
            var inputFileName = _input[fileIndex];
            if ((inputFileName ?? "").EndsWith("?"))
            {
                var concreteFileName = inputFileName.TrimEnd('?');
                _input[inputFileCount] = concreteFileName;
                OptionalInputCLIInputs.Add(concreteFileName);
            }
        }
    }

    public void LoadInputFiles()
    {
        SaveOptionalCLIInputs();
        var fs = new FileSet();
        if (!ReferenceEquals(_input, null) && _input.Any())
        {
            foreach (var input in _input)
            {
                if (!string.IsNullOrEmpty(input))
                {
                    var inputFilePatterns = input.Split(
                        ",".ToCharArray(),
                        StringSplitOptions.RemoveEmptyEntries);
                    foreach (var filePattern in inputFilePatterns)
                    {
                        ImportFile(filePattern, fs);
                    }

                    if (fs.FileSetFiles.Any())
                    {
                        InputFileContents =
                            fs.FileSetFiles.First().FileContents;
                    }
                }
            }
        }

        InputFileSetXml = fs.ToXml();
        FileSet = fs;
    }

    private static string TryFindNearestProjectRootFrom(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            return null;
        }

        try
        {
            var di = new DirectoryInfo(Path.GetFullPath(startPath));
            while (di != null)
            {
                if (File.Exists(Path.Combine(di.FullName, "effortless.json")) ||
                    File.Exists(Path.Combine(di.FullName, "ssotme.json")))
                {
                    return di.FullName;
                }

                di = di.Parent;
            }
        }
        catch
        {
            // Path may be invalid.
        }

        return null;
    }

    private static string WithTrailingSeparator(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        var last = path[path.Length - 1];
        if (last != Path.DirectorySeparatorChar &&
            last != Path.AltDirectorySeparatorChar)
        {
            return path + Path.DirectorySeparatorChar;
        }

        return path;
    }

    private static void ValidatePathIsInProjectScope(
        string filePath,
        string projectName,
        string projectRoot = null,
        List<string> allowedFolders = null)
    {
        if (string.IsNullOrEmpty(projectRoot))
        {
            return;
        }

        try
        {
            var absoluteProjectRoot =
                WithTrailingSeparator(Path.GetFullPath(projectRoot));
            var absoluteFilePath = WithTrailingSeparator(
                Path.GetFullPath(
                    Path.Combine(Environment.CurrentDirectory, filePath)));

            if (absoluteFilePath.StartsWith(
                    absoluteProjectRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (allowedFolders != null && allowedFolders.Any())
            {
                foreach (var allowedFolder in allowedFolders)
                {
                    var absoluteAllowedFolder =
                        WithTrailingSeparator(Path.GetFullPath(allowedFolder));
                    if (absoluteFilePath.StartsWith(
                            absoluteAllowedFolder,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        System.Console.WriteLine(
                            $"Allowing access to parent effortless project: {absoluteAllowedFolder}");
                        return;
                    }
                }
            }

            throw new NoStackException(
                $"Access denied: The path '{filePath}' resolves to a directory outside the current project scope ({absoluteFilePath}).\n" +
                $"Current project: {projectName} ({absoluteProjectRoot})\n" +
                "For security reasons, effortless can only access files within the project directory.");
        }
        catch (NoStackException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new NoStackException(
                $"Error validating path '{filePath}': {ex.Message}");
        }
    }

    private void ImportFile(string filePattern, FileSet fs)
    {
        var fileNameReplacement = string.Empty;
        if (filePattern.Contains("="))
        {
            fileNameReplacement =
                filePattern.Substring(0, filePattern.IndexOf("="));
            filePattern =
                filePattern.Substring(filePattern.IndexOf("=") + 1);
        }

        var directoryPath = Path.GetDirectoryName(filePattern);
        if (!string.IsNullOrEmpty(directoryPath))
        {
            var effectiveProjectRoot = _project?.RootPath ??
                                       TryFindNearestProjectRootFrom(
                                           Environment.CurrentDirectory);

            if (string.IsNullOrEmpty(effectiveProjectRoot))
            {
                throw new InvalidOperationException(
                    $"No parent 'effortless.json' found from {Environment.CurrentDirectory}");
            }

            var name = _project?.Name ??
                       new DirectoryInfo(effectiveProjectRoot).Name;

            List<string> allowedFolders = null;
            var rootDI = new DirectoryInfo(effectiveProjectRoot);
            var parentDI = rootDI.Parent;
            if (parentDI != null)
            {
                if (File.Exists(
                        Path.Combine(parentDI.FullName, "effortless.json")) ||
                    File.Exists(
                        Path.Combine(parentDI.FullName, "ssotme.json")))
                {
                    allowedFolders =
                        new List<string> { parentDI.FullName };
                }
            }

            ValidatePathIsInProjectScope(
                directoryPath,
                name,
                effectiveProjectRoot,
                allowedFolders);
        }

        var di = new DirectoryInfo(
            Path.Combine(".", Path.GetDirectoryName(filePattern)));
        filePattern = Path.GetFileName(filePattern);
        var matchingFiles = Array.Empty<FileInfo>();
        if (di.Exists)
        {
            matchingFiles = di.GetFiles(filePattern);
        }

        if (!matchingFiles.Any() &&
            !OptionalInputCLIInputs.Contains(filePattern))
        {
            var curColor = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.Yellow;
            System.Console.WriteLine(
                "\n\nWARNING:\n\n - No INPUT files matched {0} in {1}\n",
                filePattern,
                di.FullName);
            var fsf = new FileSetFile
            {
                RelativePath = Path.GetFileName(filePattern)
            };
            fs.FileSetFiles.Add(fsf);
            System.Console.ForegroundColor = curColor;
        }

        foreach (var matchingFileFI in matchingFiles)
        {
            var fsf = new FileSetFile
            {
                RelativePath = string.IsNullOrEmpty(fileNameReplacement)
                    ? matchingFileFI.Name
                    : fileNameReplacement
            };
            var projectRootLength = _project.RootPath.Length;
            var matchingFileFILength =
                matchingFileFI.Directory.FullName.Length;
            var minLength =
                Math.Min(projectRootLength, matchingFileFILength);
            fsf.OriginalRelativePath =
                matchingFileFI.FullName.Substring(minLength)
                    .Replace("\\", "/");
            fs.FileSetFiles.Add(fsf);

            if (matchingFileFI.Exists)
            {
                if (matchingFileFI.IsBinaryFile())
                {
                    fsf.ZippedBinaryFileContents =
                        File.ReadAllBytes(matchingFileFI.FullName).Zip();
                }
                else
                {
                    fsf.ZippedFileContents =
                        File.ReadAllText(matchingFileFI.FullName).Zip();
                }
            }
            else
            {
                var curColor = System.Console.ForegroundColor;
                System.Console.ForegroundColor = ConsoleColor.Yellow;
                System.Console.WriteLine(
                    "INPUT Format: {0} did not match any files in {1}",
                    filePattern,
                    di.FullName);
                System.Console.ForegroundColor = curColor;
            }
        }
    }
}

public class NoStackException : Exception
{
    public NoStackException(string message)
        : base(message)
    {
    }
}
