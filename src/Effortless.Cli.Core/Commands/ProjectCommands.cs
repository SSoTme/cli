using Effortless.Cli.Options;
using Effortless.Cli.Project;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli.Commands;

public sealed class ProjectCommands
{
    public EffortlessProject Init(CliInvocation invocation)
    {
        var directory = new DirectoryInfo(invocation.CurrentDirectory);
        var existingFile = ProjectLocator.GetProjectFileAt(
            directory,
            reverseUpdate: false);
        if (existingFile.Exists)
        {
            var existing = ProjectLocator.Load(
                existingFile,
                directory);
            if (!string.IsNullOrWhiteSpace(invocation.Options.projectName))
            {
                existing.Name = FirstWord(invocation.Options.projectName);
            }

            existing.CurrentPath ??= string.Empty;
            existing.Save();
            return existing;
        }

        var name = string.IsNullOrWhiteSpace(invocation.Options.projectName)
            ? directory.Name
            : FirstWord(invocation.Options.projectName);
        var project = new EffortlessProject
        {
            Name = name,
            RootPath = directory.FullName,
            CurrentPath = null,
        };
        project.Save(directory);
        WriteGitIgnore(directory);
        WriteEnvironmentTemplate(directory);
        WriteRulebook(directory, name);
        return project;
    }

    public int Describe(CliInvocation invocation, bool all)
    {
        if (all)
        {
            invocation.Project.Describe();
        }
        else
        {
            invocation.Project.Describe(invocation.CurrentDirectory);
        }

        return 0;
    }

    public int ListSettings(CliInvocation invocation)
    {
        invocation.Project.ListSettings();
        return 0;
    }

    public int AddSettings(CliInvocation invocation)
    {
        foreach (var setting in invocation.Options.addSetting)
        {
            invocation.Project.AddSetting(setting);
        }

        invocation.Project.Save();
        return 0;
    }

    public int RemoveSettings(CliInvocation invocation)
    {
        foreach (var setting in invocation.Options.removeSetting)
        {
            invocation.Project.RemoveSetting(setting);
        }

        invocation.Project.Save();
        return 0;
    }

    private static string FirstWord(string value) =>
        value.Split(
            new[] { ' ', '\t' },
            StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;

    private static void WriteGitIgnore(DirectoryInfo directory)
    {
        var path = Path.Combine(directory.FullName, ".gitignore");
        if (File.Exists(path))
        {
            var contents = File.ReadAllText(path);
            if (!contents.Split('\n').Any(
                    line => line.Trim().Equals(
                        "effortless.env",
                        StringComparison.Ordinal)))
            {
                File.WriteAllText(
                    path,
                    contents.TrimEnd('\r', '\n')
                    + Environment.NewLine
                    + "effortless.env"
                    + Environment.NewLine);
            }

            return;
        }

        File.WriteAllText(
            path,
            string.Join(
                Environment.NewLine,
                new[]
                {
                    "/**/obj/**/*",
                    "/**/bin/**/*",
                    "/**/.ssotme/**/*",
                    "/**/DSPXml/**/*",
                    "/SSoT/__patch.json",
                    "/**/.vs/**/*",
                    "/**/node_modules/**/*",
                    "/**/.vscode/**/*",
                    "ssotme.env",
                    "effortless.env",
                }));
    }

    private static void WriteEnvironmentTemplate(DirectoryInfo directory)
    {
        var path = Path.Combine(directory.FullName, "effortless.env");
        if (File.Exists(path))
        {
            return;
        }

        File.WriteAllText(
            path,
            """
            # Project-scoped secrets. Do not commit this file.
            # AIRTABLE_PAT=xyz
            # Example:
            # effortless airtable-to-rulebook -account airtable
            """
            + Environment.NewLine);
    }

    private static void WriteRulebook(
        DirectoryInfo directory,
        string projectName)
    {
        var rulebookDirectory = Directory.CreateDirectory(
            Path.Combine(directory.FullName, "effortless-rulebook"));
        var path = Path.Combine(
            rulebookDirectory.FullName,
            "effortless-rulebook.json");
        if (File.Exists(path))
        {
            return;
        }

        var root = new JObject
        {
            ["project"] = new JObject
            {
                ["name"] = projectName,
            },
        };
        File.WriteAllText(
            path,
            root.ToString(Formatting.Indented) + Environment.NewLine);
    }
}
