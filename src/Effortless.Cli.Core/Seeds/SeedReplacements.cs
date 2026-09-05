using System.Text;
using Effortless.Cli.Project;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli.Seeds;

/// <summary>
/// Applies the $key$ seed contract whenever a project containing
/// effortless-seed.json (or the legacy ssotme-seed.json) is loaded.
/// </summary>
public sealed class SeedReplacements : ISeedReplacements
{
    private static readonly UTF8Encoding StrictUtf8 =
        new(false, true);
    private static readonly string[] IgnoredDirectories =
        [".git", ".effortless", ".ssotme", "bin", "obj", "node_modules"];

    public Task ApplyAsync(
        DirectoryInfo rootDirectory,
        bool reverseUpdate)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        var template = new FileInfo(
            Path.Combine(
                rootDirectory.FullName,
                "effortless-seed.json"));
        if (!template.Exists)
        {
            template = new FileInfo(
                Path.Combine(
                    rootDirectory.FullName,
                    "ssotme-seed.json"));
        }

        if (!template.Exists)
        {
            return Task.CompletedTask;
        }

        var seed = JObject.Parse(
            File.ReadAllText(template.FullName));
        if (seed["replacements"] is not JArray definitions)
        {
            return Task.CompletedTask;
        }

        var configFile = new FileInfo(
            Path.Combine(
                rootDirectory.FullName,
                "seed-config-values.json"));
        var secretFile = new FileInfo(
            Path.Combine(
                rootDirectory.FullName,
                "seed-secret-values.json"));
        var parentConfigFile = rootDirectory.Parent is null
            ? null
            : new FileInfo(
                Path.Combine(
                    rootDirectory.Parent.FullName,
                    "seed-config-values.json"));
        var config = LoadValues(configFile);
        var secrets = LoadValues(secretFile);
        var parentConfig = LoadValues(parentConfigFile);
        var replacements =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions.OfType<JObject>())
        {
            var key = definition["key"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidDataException(
                    "Every seed replacement requires a non-empty key.");
            }

            var isSecret =
                definition["secret"]?.Value<bool>() == true;
            var target = isSecret ? secrets : config;
            var value = ReadValue(target, key)
                        ?? ReadValue(parentConfig, key)
                        ?? definition["default"]?.Value<string>();
            if (value is null)
            {
                value = PromptForValue(
                    key,
                    definition["description"]
                        ?.Value<string>());
                WriteValue(target, key, value);
                SaveValues(
                    isSecret ? secretFile : configFile,
                    target);
            }

            replacements[key] = value;
        }

        ReplaceInTree(
            rootDirectory,
            replacements,
            new HashSet<string>(
                [
                    template.FullName,
                    configFile.FullName,
                    secretFile.FullName,
                ],
                StringComparer.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    private static JObject LoadValues(FileInfo file)
    {
        file?.Refresh();
        if (file is null || !file.Exists)
        {
            return new JObject
            {
                ["replacements"] = new JArray(),
            };
        }

        var root = JObject.Parse(
            File.ReadAllText(file.FullName));
        root["replacements"] ??= new JArray();
        return root;
    }

    private static string ReadValue(
        JObject values,
        string key) =>
        (values?["replacements"] as JArray)
        ?.OfType<JObject>()
        .FirstOrDefault(item => string.Equals(
            item["key"]?.Value<string>(),
            key,
            StringComparison.OrdinalIgnoreCase))
        ?["value"]?.Value<string>();

    private static void WriteValue(
        JObject values,
        string key,
        string value)
    {
        var items = (JArray)values["replacements"];
        var existing = items
            .OfType<JObject>()
            .FirstOrDefault(item => string.Equals(
                item["key"]?.Value<string>(),
                key,
                StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            items.Add(
                new JObject
                {
                    ["key"] = key,
                    ["value"] = value,
                });
        }
        else
        {
            existing["value"] = value;
        }
    }

    private static void SaveValues(
        FileInfo file,
        JObject values)
    {
        File.WriteAllText(
            file.FullName,
            values.ToString(Formatting.Indented)
            + Environment.NewLine);
    }

    private static string PromptForValue(
        string key,
        string description)
    {
        if (Console.IsInputRedirected)
        {
            throw new InvalidDataException(
                $"Seed replacement '{key}' has no configured value or default.");
        }

        Console.Write(
            $"{description ?? key}{Environment.NewLine}Value: ");
        return Console.ReadLine()
               ?? throw new EndOfStreamException(
                   $"No value was supplied for seed replacement '{key}'.");
    }

    private static void ReplaceInTree(
        DirectoryInfo directory,
        IReadOnlyDictionary<string, string> replacements,
        IReadOnlySet<string> excludedFiles)
    {
        if (IgnoredDirectories.Contains(
                directory.Name,
                StringComparer.OrdinalIgnoreCase)
            || (directory.Parent is not null
                && directory.Name.StartsWith(
                    ".",
                    StringComparison.Ordinal)))
        {
            return;
        }

        foreach (var file in directory
                     .GetFiles()
                     .ToArray())
        {
            if (excludedFiles.Contains(file.FullName)
                || file.Length > 100_000)
            {
                continue;
            }

            ReplaceInFile(
                file,
                replacements);
        }

        foreach (var child in directory
                     .GetDirectories()
                     .ToArray())
        {
            ReplaceInTree(
                child,
                replacements,
                excludedFiles);
        }
    }

    private static void ReplaceInFile(
        FileInfo original,
        IReadOnlyDictionary<string, string> replacements)
    {
        byte[] bytes;
        string contents;
        try
        {
            bytes = File.ReadAllBytes(original.FullName);
            contents = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return;
        }

        var updated = contents;
        var targetName = original.Name;
        foreach (var replacement in replacements)
        {
            var token = $"${replacement.Key}$";
            updated = updated.Replace(
                token,
                replacement.Value,
                StringComparison.OrdinalIgnoreCase);
            targetName = targetName.Replace(
                token,
                replacement.Value,
                StringComparison.OrdinalIgnoreCase);
        }

        if (!string.Equals(
                updated,
                contents,
                StringComparison.Ordinal))
        {
            File.WriteAllText(
                original.FullName,
                updated,
                new UTF8Encoding(false));
        }

        if (string.Equals(
                targetName,
                original.Name,
                StringComparison.Ordinal))
        {
            return;
        }

        var targetPath = Path.Combine(
            original.DirectoryName!,
            targetName);
        if (File.Exists(targetPath))
        {
            throw new IOException(
                $"Seed replacement target already exists: {targetPath}");
        }

        original.MoveTo(targetPath);
    }
}
