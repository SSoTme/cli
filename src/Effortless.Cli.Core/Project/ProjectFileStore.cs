using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli.Project;

public static class ProjectFileStore
{
    public static void Save(EffortlessProject project)
    {
        Save(project, new DirectoryInfo(project.RootPath));
    }

    public static void Save(
        EffortlessProject project,
        DirectoryInfo rootDirectory)
    {
        project.RemoveUUIds();
        project.AddSetting(string.Format("project-name={0}", project.Name));

        var projectFileName = GetProjectFileName(project, false);
        JObject existingJson = null;

        if (File.Exists(projectFileName))
        {
            try
            {
                var existingContent = File.ReadAllText(projectFileName);
                existingJson = JObject.Parse(existingContent);
            }
            catch
            {
                existingJson = null;
            }
        }

        var projectJson = JsonConvert.SerializeObject(
            project,
            Formatting.Indented);
        var newJson = JObject.Parse(projectJson);

        newJson.Remove("RootPath");
        if (newJson["ExpandedPaths"] != null
            && !newJson["ExpandedPaths"].Any())
        {
            newJson.Remove("ExpandedPaths");
        }

        if (newJson["HiddenPaths"] != null
            && !newJson["HiddenPaths"].Any())
        {
            newJson.Remove("HiddenPaths");
        }

        if (existingJson != null
            && existingJson["ProjectTranspilers"] != null
            && newJson["ProjectTranspilers"] != null)
        {
            var existingTranspilers =
                existingJson["ProjectTranspilers"] as JArray;
            var newTranspilers =
                newJson["ProjectTranspilers"] as JArray;

            if (existingTranspilers != null && newTranspilers != null)
            {
                for (var index = 0; index < newTranspilers.Count; index++)
                {
                    var newTranspiler = newTranspilers[index] as JObject;
                    if (newTranspiler == null)
                    {
                        continue;
                    }

                    var name = newTranspiler["Name"]?.ToString();
                    var relativePath =
                        newTranspiler["RelativePath"]?.ToString();
                    var transpilerGroup =
                        newTranspiler["TranspilerGroup"]?.ToString();

                    var existingMatch = existingTranspilers
                        .Cast<JObject>()
                        .FirstOrDefault(transpiler =>
                            transpiler["Name"]?.ToString() == name
                            && transpiler["RelativePath"]?.ToString()
                                == relativePath
                            && transpiler["TranspilerGroup"]?.ToString()
                                == transpilerGroup);

                    if (existingMatch == null)
                    {
                        continue;
                    }

                    var managedKeys = new HashSet<string>(
                        typeof(ProjectTranspiler)
                            .GetProperties(
                                BindingFlags.Public
                                | BindingFlags.Instance)
                            .Select(property => property.Name),
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var property in existingMatch.Properties())
                    {
                        if (managedKeys.Contains(property.Name))
                        {
                            continue;
                        }

                        if (newTranspiler[property.Name] == null)
                        {
                            newTranspiler[property.Name] = property.Value;
                        }
                    }
                }
            }
        }

        projectJson = newJson.ToString(Formatting.Indented);
        projectJson = $"{projectJson}{Environment.NewLine}";

        var count = 0;
        while (count < 5)
        {
            try
            {
                File.WriteAllText(projectFileName, projectJson);
                break;
            }
            catch (IOException)
            {
                count++;
                Thread.Sleep(500);
            }
        }
    }

    public static string GetProjectFileName(
        EffortlessProject project,
        bool reverseUpdate)
    {
        return GetProjectFI(project, reverseUpdate).FullName;
    }

    public static FileInfo GetProjectFI(
        EffortlessProject project,
        bool reverseUpdate)
    {
        return ProjectLocator.GetProjectFIAt(
            new DirectoryInfo(project.RootPath),
            reverseUpdate);
    }
}
