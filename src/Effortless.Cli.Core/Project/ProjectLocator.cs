using System.Threading;
using Newtonsoft.Json;

namespace Effortless.Cli.Project;

public interface ISeedReplacements
{
    Task ApplyAsync(DirectoryInfo rootDirectory, bool reverseUpdate);
}

internal sealed class NoOpSeedReplacements : ISeedReplacements
{
    public Task ApplyAsync(
        DirectoryInfo rootDirectory,
        bool reverseUpdate)
    {
        return Task.CompletedTask;
    }
}

public static class ProjectLocator
{
    private static readonly ISeedReplacements DefaultSeedReplacements =
        new NoOpSeedReplacements();

    internal static bool IsValidProjectFile(FileInfo file)
    {
        if (!file.Exists)
        {
            return false;
        }

        try
        {
            var content = File.ReadAllText(file.FullName);
            return content.Contains("\"ProjectTranspilers\"");
        }
        catch
        {
            return false;
        }
    }

    public static FileInfo GetProjectFIAt(
        DirectoryInfo rootDirectory,
        bool reverseUpdate,
        ISeedReplacements seedReplacements = null)
    {
        seedReplacements ??= DefaultSeedReplacements;

        var effortlessFile = new FileInfo(
            Path.Combine(rootDirectory.FullName, "effortless.json"));
        var ssotmeFile = new FileInfo(
            Path.Combine(rootDirectory.FullName, "ssotme.json"));

        if (!effortlessFile.Exists && IsValidProjectFile(ssotmeFile))
        {
            File.Move(ssotmeFile.FullName, effortlessFile.FullName);
            effortlessFile.Refresh();
        }

        var ssotmeEnvironmentFile = new FileInfo(
            Path.Combine(rootDirectory.FullName, "ssotme.env"));
        var effortlessEnvironmentFile = new FileInfo(
            Path.Combine(rootDirectory.FullName, "effortless.env"));
        if (!effortlessEnvironmentFile.Exists
            && ssotmeEnvironmentFile.Exists)
        {
            File.Move(
                ssotmeEnvironmentFile.FullName,
                effortlessEnvironmentFile.FullName);
        }

        var candidates = new[]
        {
            effortlessFile,
            ssotmeFile,
            new FileInfo(
                Path.Combine(rootDirectory.FullName, "aicapture.json")),
            new FileInfo(
                Path.Combine(rootDirectory.FullName, "SSoTmeProject.json")),
        };

        var projectFile = candidates.FirstOrDefault(IsValidProjectFile);
        if (projectFile is null)
        {
            return effortlessFile;
        }

        var task = Task.Run(
            () => seedReplacements.ApplyAsync(
                projectFile.Directory,
                reverseUpdate));
        try
        {
            task.Wait();
            if (task.Exception is not null)
            {
                throw task.Exception;
            }
        }
        catch (Exception exception)
        {
            throw new ThreadInterruptedException(
                $"Error applying seed replacements....{exception.Message}",
                exception);
        }

        return projectFile;
    }

    public static FileInfo GetProjectFileAt(
        DirectoryInfo rootDirectory,
        bool reverseUpdate,
        ISeedReplacements seedReplacements = null)
    {
        return GetProjectFIAt(
            rootDirectory,
            reverseUpdate,
            seedReplacements);
    }

    internal static EffortlessProject Load(
        FileInfo projectFile,
        DirectoryInfo requestDirectory = null,
        bool updateCurrent = true)
    {
        var count = 0;
        while (count++ < 10)
        {
            try
            {
                var projectJson = File.ReadAllText(projectFile.FullName);
                var project =
                    JsonConvert.DeserializeObject<EffortlessProject>(
                        projectJson);
                project.RootPath = projectFile.Directory.FullName;
                if (string.IsNullOrEmpty(project.Name))
                {
                    project.Name = Path.GetFileName(project.RootPath);
                }

                var hadNoId =
                    string.IsNullOrEmpty(project.SSoTmeProjectId)
                    || project.SSoTmeProjectId == Guid.Empty.ToString();
                project.GenerateNewProject();
                if (hadNoId)
                {
                    ProjectFileStore.Save(project, projectFile.Directory);
                }

                if (updateCurrent)
                {
                    project.CurrentPath =
                        project.GetProjectRelativePath(requestDirectory);
                }

                return project;
            }
            catch (IOException)
            {
                Thread.Sleep(500);
            }
        }

        throw new Exception(
            "Unable to load project file: " + projectFile.FullName);
    }

    public static EffortlessProject LoadOrFail(
        DirectoryInfo directoryToCheck,
        bool updateCurrent = true,
        bool reverseUpdate = false,
        ISeedReplacements seedReplacements = null)
    {
        return TryToLoad(
            directoryToCheck,
            directoryToCheck,
            updateCurrent,
            reverseUpdate,
            seedReplacements);
    }

    public static EffortlessProject TryToLoad(
        DirectoryInfo directoryToCheck,
        DirectoryInfo requestDirectory = null,
        bool updateCurrent = true,
        bool reverseUpdate = false,
        ISeedReplacements seedReplacements = null)
    {
        var projectFile = GetProjectFIAt(
            directoryToCheck,
            reverseUpdate,
            seedReplacements);

        if (ReferenceEquals(requestDirectory, null))
        {
            requestDirectory = directoryToCheck;
        }

        if (projectFile.Exists)
        {
            return Load(projectFile, requestDirectory, updateCurrent);
        }

        if (ReferenceEquals(directoryToCheck.Parent, null))
        {
            return null;
        }

        return TryToLoad(
            directoryToCheck.Parent,
            requestDirectory,
            updateCurrent,
            seedReplacements: seedReplacements);
    }

    public static string FindNearestProjectRoot()
    {
        try
        {
            var directory = new DirectoryInfo(Environment.CurrentDirectory);
            while (directory != null)
            {
                if (File.Exists(
                        Path.Combine(
                            directory.FullName,
                            "effortless.json"))
                    || File.Exists(
                        Path.Combine(
                            directory.FullName,
                            "ssotme.json"))
                    || File.Exists(
                        Path.Combine(
                            directory.FullName,
                            "aicapture.json")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }
        catch
        {
        }

        return null;
    }
}
