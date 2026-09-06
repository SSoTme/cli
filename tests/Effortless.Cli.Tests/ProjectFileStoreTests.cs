using System.ComponentModel;
using Effortless.Cli.Project;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli.Tests;

public sealed class ProjectFileStoreTests
{
    [Fact(DisplayName = "unit-project-save-merge: project saves preserve only custom omitted properties")]
    public void ProjectSavesPreserveOnlyCustomOmittedProperties()
    {
        using var directory = new TestDirectory();
        var projectPath = directory.File("effortless.json");
        File.WriteAllText(
            projectPath,
            """
            {
              "Name": "Demo",
              "ProjectSettings": [],
              "ProjectTranspilers": [
                {
                  "Name": "Echo",
                  "RelativePath": "/generated",
                  "CommandLine": "echo",
                  "TranspilerGroup": "docs",
                  "PinnedVersion": "v2025.01.01.0001",
                  "Description": "hand written",
                  "Enabled": true
                }
              ]
            }
            """);

        var project = new EffortlessProject
        {
            Name = "Demo",
            RootPath = directory.Path,
            ExpandedPaths = [],
            HiddenPaths = [],
            ProjectSettings = new BindingList<ProjectSetting>(),
            ProjectTranspilers = new BindingList<ProjectTranspiler>
            {
                new()
                {
                    Name = "Echo",
                    RelativePath = "/generated",
                    CommandLine = "echo",
                    TranspilerGroup = "docs",
                    PinnedVersion = null,
                    LastVersionUsed = "v2026.08.30.1725",
                },
            },
        };

        ProjectFileStore.Save(project);

        var savedText = File.ReadAllText(projectPath);
        var saved = JObject.Parse(savedText);
        var transpiler = Assert.IsType<JObject>(
            Assert.Single((JArray)saved["ProjectTranspilers"]!));

        Assert.EndsWith(Environment.NewLine, savedText);
        Assert.Null(saved["RootPath"]);
        Assert.Null(saved["ExpandedPaths"]);
        Assert.Null(saved["HiddenPaths"]);
        Assert.Null(transpiler["PinnedVersion"]);
        Assert.Equal("v2026.08.30.1725", (string?)transpiler["LastVersionUsed"]);
        Assert.Equal("hand written", (string?)transpiler["Description"]);
        Assert.True((bool?)transpiler["Enabled"]);
        Assert.NotNull(transpiler["IsDisabled"]);
        Assert.NotNull(transpiler["IsSSoTTranspiler"]);
        Assert.Contains(
            project.ProjectSettings,
            setting => setting.Name == "project-name" && setting.Value == "Demo");
    }
}
