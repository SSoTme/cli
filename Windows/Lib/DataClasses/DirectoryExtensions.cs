using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Threading;

public static class DirectoryExtensions
{
    public static async Task ApplySeedReplacementsAsync(this DirectoryInfo rootPath, bool reverseApply = false)
    {
        try
        {
            // Load replacements from the seed file
            var replacements = await LoadReplacementsAsync(rootPath, reverseApply);
            if (replacements == null) return;

            if (!RequiresReplacements(rootPath, reverseApply)) return;

            //Console.WriteLine("Needs replacements");

            // Apply replacements recursively to all files
            foreach (JObject replacement in replacements)
            {
                if (reverseApply)
                {
                    //Console.WriteLine($"REVERSE-REPLACING: {replacement.ToString()}");
                    var findText = $"{replacement["default-replacement-text"]}";
                    var replaceText = $"{replacement["find-text"]}";
                    await ReplaceInFiles(rootPath, findText, replaceText);
                }
                else
                {
                    //Console.WriteLine($"REPLACING: {replacement.ToString()}");
                    var findText = $"{replacement["find-text"]}";
                    var replaceText = $"{replacement["replacement-text"]}";
                    await ReplaceInFiles(rootPath, findText, replaceText);
                }
            }
        }
        finally
        {
            ToggleRequiresReplacements(rootPath, reverseApply);
        }
    }

    private static async Task<JArray> LoadReplacementsAsync(DirectoryInfo rootPath, bool reverseUpdate)
    {
        var seedFilePath = Path.Combine(rootPath.FullName, "ssotme-seed.json");
        if (!File.Exists(seedFilePath)) return null;

        var json = File.ReadAllText(seedFilePath);
        JObject seedDetails = JObject.Parse(json);
        await UpdateReplacementTexts(rootPath.FullName, seedFilePath, seedDetails, reverseUpdate);

        JArray replacements = (JArray)seedDetails["replacements"];

        return replacements;
    }

    private static async Task UpdateReplacementTexts(string rootPath, string seedFilePath, JObject seedDetails, bool reverseUpdate)
    {
        bool changed = false;
        JArray replacements = seedDetails["replacements"] as JArray;
        foreach (JObject replacement in replacements)
        {
            var findText = $"{replacement["find-text"]}";
            var defaultReplacementText = $"{replacement["default-replacement-text"]}";
            var replacementText = $"{replacement["replacement-text"]}";

            // Check if replacement-text is empty and offer to use default-replacement-text
            if (String.IsNullOrEmpty(replacementText) || String.Equals(replacementText, findText, StringComparison.OrdinalIgnoreCase))
            {
                if (!reverseUpdate)
                {
                    changed = EnsureReplacementTextIsPopulated(rootPath, replacement, findText, defaultReplacementText, replacementText);
                }
            }
            else if (reverseUpdate)
            {
                replacement["default-replacement-text"] = replacementText ?? defaultReplacementText;
                replacement["replacement-text"] = findText;
                changed = true;
            }
        }

        if (changed)
        {
            // Save updated JSON back to file
            seedDetails["requires-replacements"] = true;
            File.WriteAllText(seedFilePath, seedDetails.ToString());
        }
    }

    private static bool EnsureReplacementTextIsPopulated(string rootPath, JObject replacement, string findText, string defaultReplacementText, string replacementText)
    {
        string newText = defaultReplacementText;

        if (IsUnusedReplacement(rootPath, findText)) return false;
        
        Console.WriteLine($"{replacement["description"]}\n'{findText}' is '{defaultReplacementText}'.\nEnter new text or press ENTER to use the default:");
        var userInput = Console.ReadLine();
        
        if (!String.IsNullOrEmpty(userInput)) newText = userInput;

        if (String.Equals(newText, replacementText)) return true;
        
        replacement["replacement-text"] = newText;

        return false;
    }

    private static bool IsUnusedReplacement(string rootPath, string findText)
    {
        DirectoryInfo rootDI = new DirectoryInfo(rootPath);

        // Check if the directory exists
        if (!rootDI.Exists)
        {
            throw new DirectoryNotFoundException($"The directory {rootPath} was not found.");
        }

        // Recursively check all files and subdirectories for the findText
        bool unused = IsTextUnusedInDirectory(rootDI, findText);
        return unused;
    }

    private static bool IsTextUnusedInDirectory(DirectoryInfo directoryInfo, string findText)
    {
        // Check each file in the directory
        foreach (FileInfo file in directoryInfo.GetFiles())
        {
            if (file.Name == "ssotme-seed.json") continue;
            // Open each file and search for the text
            if (FileContainsText(file.FullName, findText))
            {
                return false; // Text is found, no need to check further
            }
        }

        // Recursively check each subdirectory
        foreach (DirectoryInfo subDir in directoryInfo.GetDirectories())
        {
            if (!IsTextUnusedInDirectory(subDir, findText))
            {
                return false; // Text is found in a subdirectory
            }
        }

        // If text is not found in any files or subdirectories
        return true;
    }

    private static bool FileContainsText(string filePath, string findText)
    {
        try
        {
            // Read all text in the file and search for the text
            string content = File.ReadAllText(filePath);
            return content.Contains(findText);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while reading the file {filePath}: {ex.Message}");
            return false; // If an error occurs, assume the text does not exist in this file
        }
    }

    private static async Task ReplaceInFiles(DirectoryInfo rootPath, string findText, string replaceText)
    {
        foreach (var file in rootPath.GetFiles("*", SearchOption.AllDirectories))
        {
            if (file.Name == "ssotme-seed.json") continue;
            var content = File.ReadAllText(file.FullName);
            var newContent = Regex.Replace(content, Regex.Escape(findText), replaceText);

            if (newContent != content)
            {
                File.WriteAllText(file.FullName, newContent);
            }
        }
    }

    private static bool RequiresReplacements(DirectoryInfo rootPath, bool reverseApply)
    {
        var seedFilePath = Path.Combine(rootPath.FullName, "ssotme-seed.json");
        if (!File.Exists(seedFilePath)) return false;
        var json = File.ReadAllText(seedFilePath);
        var seedDetails = JObject.Parse(json);

        // Toggle the requires-replacements based on the reverseApply parameter
        bool currentlyRequiresReplacement = (bool)seedDetails["requires-replacements"];
        var requiresReplacement = currentlyRequiresReplacement || reverseApply;
        //Console.WriteLine($"REPLACEMENT JUDGEMENT: json.Requires Replacement = {currentlyRequiresReplacement} and {(reverseApply ? "is a reverse replace" : "")}");
        return requiresReplacement;
    }


    private static void ToggleRequiresReplacements(DirectoryInfo rootPath, bool reverseApply)
    {
        var seedFilePath = Path.Combine(rootPath.FullName, "ssotme-seed.json");
        if (!File.Exists(seedFilePath)) return;
        var json = File.ReadAllText(seedFilePath);
        var seedDetails = JObject.Parse(json);

        // Toggle the requires-replacements based on the reverseApply parameter
        bool requiresReplacement = (bool)seedDetails["requires-replacements"];
        if (requiresReplacement == reverseApply) return;
        seedDetails["requires-replacements"] = reverseApply;
        File.WriteAllText(seedFilePath, $"{seedDetails}");
    }
}
