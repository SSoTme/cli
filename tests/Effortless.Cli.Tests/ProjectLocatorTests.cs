using Effortless.Cli.Project;

namespace Effortless.Cli.Tests;

public sealed class ProjectLocatorTests
{
    [Fact(DisplayName = "unit-project-locator: project candidates preserve precedence and migration")]
    public void ProjectCandidatesPreservePrecedenceAndMigration()
    {
        using (var precedence = new TestDirectory())
        {
            WriteProject(precedence.File("effortless.json"), "Effortless");
            WriteProject(precedence.File("ssotme.json"), "Legacy");

            var selected = ProjectLocator.GetProjectFileAt(
                new DirectoryInfo(precedence.Path),
                reverseUpdate: false);

            Assert.Equal("effortless.json", selected.Name);
        }

        using (var migration = new TestDirectory())
        {
            WriteProject(migration.File("ssotme.json"), "Migrated");
            File.WriteAllText(migration.File("ssotme.env"), "KEY=value" + Environment.NewLine);

            var selected = ProjectLocator.GetProjectFileAt(
                new DirectoryInfo(migration.Path),
                reverseUpdate: false);

            Assert.Equal("effortless.json", selected.Name);
            Assert.True(File.Exists(migration.File("effortless.json")));
            Assert.False(File.Exists(migration.File("ssotme.json")));
            Assert.True(File.Exists(migration.File("effortless.env")));
            Assert.False(File.Exists(migration.File("ssotme.env")));
        }

        using (var invalidPrimary = new TestDirectory())
        {
            File.WriteAllText(invalidPrimary.File("effortless.json"), """{"Name":"invalid"}""");
            WriteProject(invalidPrimary.File("ssotme.json"), "Legacy");

            var selected = ProjectLocator.GetProjectFileAt(
                new DirectoryInfo(invalidPrimary.Path),
                reverseUpdate: false);

            Assert.Equal("ssotme.json", selected.Name);
        }

        using (var fallbackCandidate = new TestDirectory())
        {
            WriteProject(fallbackCandidate.File("aicapture.json"), "AICapture");
            var selected = ProjectLocator.GetProjectFileAt(
                new DirectoryInfo(fallbackCandidate.Path),
                reverseUpdate: false);
            Assert.Equal("aicapture.json", selected.Name);
        }

        using (var nested = new TestDirectory())
        {
            WriteProject(nested.File("effortless.json"), "Nested");
            var child = nested.File("one/two");
            Directory.CreateDirectory(child);

            var project = ProjectLocator.TryToLoad(new DirectoryInfo(child));

            Assert.NotNull(project);
            Assert.Equal(nested.Path, project!.RootPath);
            Assert.Equal("/one/two", project.CurrentPath);
        }
    }

    private static void WriteProject(string path, string name)
    {
        File.WriteAllText(
            path,
            $$"""
            {
              "SSoTmeProjectId": "{{Guid.NewGuid()}}",
              "Name": "{{name}}",
              "ProjectSettings": [],
              "ProjectTranspilers": []
            }
            """);
    }
}
