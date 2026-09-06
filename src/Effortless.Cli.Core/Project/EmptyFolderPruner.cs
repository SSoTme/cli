namespace Effortless.Cli.Project;

public class EmptyFolderPruner
{
    private readonly EffortlessProject _project;

    public EmptyFolderPruner(EffortlessProject project)
    {
        _project = project;
    }

    public static void RemoveEmptyFolders(
        EffortlessProject project,
        string pathToClean)
    {
        new EmptyFolderPruner(project)
            .RemoveEmptyFolders(pathToClean);
    }

    public void CleanEmptyDirectories(string path)
    {
        RemoveEmptyFolders(path);
    }

    internal void RemoveEmptyFolders(string pathToClean)
    {
        string originalCwd = null;
        try
        {
            originalCwd = NormalizeForComparison(
                Environment.CurrentDirectory);
        }
        catch
        {
        }

        var projectRoot = NormalizeForComparison(_project.RootPath);
        var protectedPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        if (_project.ProjectTranspilers != null)
        {
            foreach (var projectTranspiler in
                     _project.ProjectTranspilers)
            {
                if (String.IsNullOrEmpty(
                        projectTranspiler.RelativePath))
                {
                    continue;
                }

                var projectTranspilerPath = NormalizeForComparison(
                    Path.Combine(
                        _project.RootPath,
                        projectTranspiler.RelativePath.Trim(
                            "\\/".ToCharArray())));
                var current = projectTranspilerPath;
                while (!String.IsNullOrEmpty(current) &&
                       !String.Equals(
                           current,
                           projectRoot,
                           StringComparison.OrdinalIgnoreCase) &&
                       current.StartsWith(
                           projectRoot,
                           StringComparison.OrdinalIgnoreCase))
                {
                    protectedPaths.Add(current);
                    current = Path.GetDirectoryName(current);
                }
            }
        }

        RemoveEmptyFoldersInternal(
            pathToClean,
            originalCwd,
            protectedPaths);
    }

    private void RemoveEmptyFoldersInternal(
        string pathToClean,
        string originalCwd,
        HashSet<string> protectedPaths)
    {
        var cleanDirectory = new DirectoryInfo(pathToClean);

        if (!cleanDirectory.Exists)
        {
            return;
        }

        if ((cleanDirectory.Attributes &
             FileAttributes.ReparsePoint) != 0)
        {
            return;
        }

        List<DirectoryInfo> cleanChildDirectories;
        try
        {
            cleanChildDirectories =
                cleanDirectory.GetDirectories().ToList();
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        foreach (var childDirectory in cleanChildDirectories)
        {
            RemoveEmptyFoldersInternal(
                childDirectory.FullName,
                originalCwd,
                protectedPaths);

            childDirectory.Refresh();
            if (!childDirectory.Exists)
            {
                continue;
            }

            if ((childDirectory.Attributes &
                 FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            DirectoryInfo[] directories;
            FileInfo[] files;
            try
            {
                directories = childDirectory.GetDirectories();
                if (directories.Any())
                {
                    continue;
                }

                files = childDirectory.GetFiles();
                if (files.Any())
                {
                    continue;
                }
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            var childFullName = NormalizeForComparison(
                childDirectory.FullName);
            if (String.Equals(
                    childFullName,
                    NormalizeForComparison(_project.RootPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    $"ERROR: Cannot delete project root directory: {_project.RootPath}");
            }

            if (protectedPaths.Contains(childFullName))
            {
                continue;
            }

            if (!String.IsNullOrEmpty(originalCwd))
            {
                if (String.Equals(
                        childFullName,
                        originalCwd,
                        StringComparison.OrdinalIgnoreCase) ||
                    originalCwd.StartsWith(
                        childFullName +
                        Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            try
            {
                childDirectory.Delete();
            }
            catch (IOException)
            {
                // May not be able to delete on some platforms.
            }
        }

        cleanDirectory.Refresh();
        try
        {
            if (cleanDirectory.GetDirectories().Any())
            {
                return;
            }

            if (cleanDirectory.GetFiles().Any())
            {
                return;
            }
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        var cleanFullName = NormalizeForComparison(
            cleanDirectory.FullName);
        if (String.Equals(
                cleanFullName,
                NormalizeForComparison(_project.RootPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception(
                $"ERROR: Cannot delete project root directory: {_project.RootPath}");
        }

        if (protectedPaths.Contains(cleanFullName))
        {
            return;
        }

        if (!String.IsNullOrEmpty(originalCwd))
        {
            if (String.Equals(
                    cleanFullName,
                    originalCwd,
                    StringComparison.OrdinalIgnoreCase) ||
                originalCwd.StartsWith(
                    cleanFullName + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        try
        {
            cleanDirectory.Delete();
        }
        catch (IOException)
        {
            // May not be able to delete on some platforms.
        }
    }

    private static string NormalizeForComparison(string path)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        if (OperatingSystem.IsMacOS())
        {
            if (String.Equals(fullPath, "/var", StringComparison.Ordinal) ||
                fullPath.StartsWith("/var/", StringComparison.Ordinal) ||
                String.Equals(fullPath, "/tmp", StringComparison.Ordinal) ||
                fullPath.StartsWith("/tmp/", StringComparison.Ordinal))
            {
                fullPath = "/private" + fullPath;
            }
        }

        return fullPath;
    }
}
