using System.ComponentModel;
using Effortless.Cli.Project;

namespace Effortless.Cli.Tests;

public sealed class EmptyFolderPrunerTests
{
    [Fact(DisplayName = "unit-empty-folder-pruner: protected, current, and reparse paths survive pruning")]
    public void ProtectedCurrentAndReparsePathsSurvivePruning()
    {
        using var directory = new TestDirectory();
        using var linkedTarget = new TestDirectory();
        var protectedPath = directory.File("protected/output");
        var removablePath = directory.File("remove/me");
        var currentPath = directory.File("current/deep");
        Directory.CreateDirectory(protectedPath);
        Directory.CreateDirectory(removablePath);
        Directory.CreateDirectory(currentPath);

        var project = new EffortlessProject
        {
            RootPath = directory.Path,
            ProjectTranspilers = new BindingList<ProjectTranspiler>
            {
                new() { RelativePath = "/protected/output" },
            },
        };

        string? linkPath = null;
        if (!OperatingSystem.IsWindows())
        {
            linkPath = directory.File("linked");
            Directory.CreateSymbolicLink(linkPath, linkedTarget.Path);
        }

        var original = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = currentPath;
            EmptyFolderPruner.RemoveEmptyFolders(project, directory.Path);
        }
        finally
        {
            Environment.CurrentDirectory = original;
        }

        Assert.True(Directory.Exists(directory.Path));
        Assert.True(Directory.Exists(protectedPath));
        Assert.False(Directory.Exists(removablePath));
        Assert.True(Directory.Exists(currentPath));
        if (linkPath is not null)
        {
            Assert.True(Directory.Exists(linkPath));
            Assert.True(Directory.Exists(linkedTarget.Path));
        }
    }
}
