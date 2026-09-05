using Effortless.Cli.Options;
using Effortless.Cli.Project;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli.Commands;

/// <summary>
/// D12: the four scopes every project-scoped verb family supports.
/// </summary>
public enum DescribeScope
{
    Downstream,
    Local,
    All,
    WithSubprojects,
}

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

    public int Describe(CliInvocation invocation, DescribeScope scope)
    {
        switch (scope)
        {
            case DescribeScope.Local:
                invocation.Project.Describe(
                    invocation.CurrentDirectory,
                    exactMatch: true);
                break;
            case DescribeScope.All:
                invocation.Project.Describe();
                break;
            case DescribeScope.WithSubprojects:
                invocation.Project.Describe();
                foreach (var nested in NestedProjectFinder.Find(
                             invocation.Project.RootPath))
                {
                    nested.InvokeSSoTmeDescribe();
                }

                break;
            default:
                invocation.Project.Describe(invocation.CurrentDirectory);
                break;
        }

        return 0;
    }

    /// <summary>
    /// D22: enable/disable flip <c>IsDisabled</c> on a registered step,
    /// targeted exactly the way uninstall targets one — tool name in the
    /// current folder plus an optional transpiler group.
    /// </summary>
    public int SetStepDisabled(CliInvocation invocation, bool disabled)
    {
        var name = invocation.RawTranspilerArg
            ?? invocation.RemainingArguments.FirstOrDefault();
        if (string.IsNullOrEmpty(name))
        {
            CliLog.LogLine(
                $"Please specify a transpiler name to {(disabled ? "disable" : "enable")}",
                ConsoleColor.Red);
            return -1;
        }

        var project = invocation.Project;
        var relativePath = project.GetProjectRelativePath(
            invocation.CurrentDirectory);
        var matches = project.FindMatchingTranspilers(
            name,
            relativePath,
            invocation.Options.transpilerGroup);

        // At the project root GetProjectRelativePath yields "" while a step
        // stores "/". Retry once on the normalized form so enable/disable
        // work in the root folder.
        if (matches.Count == 0)
        {
            var normalized = "/" + relativePath
                .Replace('\\', '/')
                .Trim('/');
            if (!string.Equals(
                    normalized,
                    relativePath,
                    StringComparison.Ordinal))
            {
                matches = project.FindMatchingTranspilers(
                    name,
                    normalized,
                    invocation.Options.transpilerGroup);
                if (matches.Count > 0)
                {
                    relativePath = normalized;
                }
            }
        }

        if (matches.Count > 1)
        {
            CliLog.LogLine(
                $"Warning: '{name}' matched multiple tools. Provide a fully-qualified name to target a specific tool.",
                ConsoleColor.Yellow);
            foreach (var match in matches)
            {
                CliLog.LogLine(
                    $"  - {match.CommandLine}",
                    ConsoleColor.Yellow);
            }

            return 0;
        }

        if (matches.Count == 0)
        {
            CliLog.LogLine(
                $"No tools matching tool name '{name}' in path '{relativePath}'",
                ConsoleColor.Yellow);
            return 0;
        }

        var step = matches[0];
        step.IsDisabled = disabled;
        project.Save();
        CliLog.LogLine(
            $"{(disabled ? "Disabled" : "Enabled")} {name}",
            disabled ? ConsoleColor.Yellow : ConsoleColor.Green);
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
