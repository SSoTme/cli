using System.Xml;
using Effortless.Cli.Project;
using Effortless.Cli.Text;

namespace Effortless.Cli.FileSets;

public static class ZfsLedger
{
    public static FileInfo GetZFSFI(
        EffortlessProject project,
        string transpilerKey,
        string cwd)
    {
        string relPath = GetCurrentRelativePath(
            project,
            transpilerKey,
            cwd);
        var ledgerDI = project.GetEffortlessDI();

        var zfsFileName = string.Format(
            "{0}/{1}/{2}.zfs",
            ledgerDI.FullName,
            relPath,
            transpilerKey);
        return new FileInfo(zfsFileName);
    }

    public static void SavePreviousFileSet(
        EffortlessProject project,
        string transpilerKey,
        string cwd,
        string fileSetXml,
        bool debug = false)
    {
        if (project is null)
        {
            return;
        }

        var zfsFI = GetZFSFI(project, transpilerKey, cwd);
        if (!zfsFI.Directory.Exists)
        {
            zfsFI.Directory.Create();
        }

        File.WriteAllBytes(zfsFI.FullName, fileSetXml.Zip());

        if (debug)
        {
            var debugXmlPath = Path.Combine(
                zfsFI.Directory.FullName,
                $"{transpilerKey}.xml");
            File.WriteAllText(debugXmlPath, fileSetXml);
            System.Console.WriteLine(
                $"DEBUG: Saved transpiler XML output to: {debugXmlPath}");
        }
    }

    public static string GetCurrentRelativePath(
        EffortlessProject project,
        string transpilerKey,
        string cwd)
    {
        string curDir = string.Empty;
        try
        {
            curDir = cwd ?? Environment.CurrentDirectory;
        }
        catch (Exception)
        {
            curDir = string.Empty;
        }

        string relPath;
        if (!string.IsNullOrEmpty(curDir) &&
            curDir.StartsWith(
                project.RootPath,
                StringComparison.OrdinalIgnoreCase))
        {
            relPath = curDir.Substring(project.RootPath.Length);
            relPath = relPath.Replace('\\', '/').TrimStart('/');
        }
        else
        {
            relPath = "";
        }

        return relPath;
    }

    /// <summary>
    /// Guards against a TOCTOU race: if a file was read as tool input and the tool's
    /// response wants to overwrite that same path with OverwriteMode=Always, the file
    /// must still match what was originally read, or the on-disk edit that happened
    /// while the tool was running would be silently clobbered.
    /// </summary>
    public static void ValidateSelfSourceOverwrites(
        EffortlessProject project,
        string inputFileSetXml,
        string fileSetXml,
        string extractToDir)
    {
        if (project is null || string.IsNullOrEmpty(inputFileSetXml))
        {
            return;
        }

        if (string.IsNullOrEmpty(fileSetXml) || !fileSetXml.Contains("<"))
        {
            return;
        }

        Dictionary<string, FileSetFile> inputByFullPath;
        try
        {
            inputByFullPath = inputFileSetXml
                .ToFileSet()
                .FileSetFiles
                .Where(fsf => !string.IsNullOrEmpty(fsf.OriginalRelativePath))
                .GroupBy(
                    fsf => new FileInfo(
                            Path.Combine(
                                project.RootPath,
                                fsf.OriginalRelativePath.Trim("\\/".ToCharArray())))
                        .FullName,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return;
        }

        if (!inputByFullPath.Any())
        {
            return;
        }

        var doc = new XmlDocument();
        var trimmedXml = fileSetXml.Substring(fileSetXml.IndexOf("<"));
        doc.LoadXml(trimmedXml);
        if (doc.DocumentElement is null ||
            doc.DocumentElement.Name != "FileSet")
        {
            return;
        }

        foreach (XmlElement fsfElem in
                 doc.DocumentElement.SelectNodes("//FileSetFile"))
        {
            if (!IsAlwaysOverwriteElement(fsfElem))
            {
                continue;
            }

            foreach (XmlElement relPathElem in
                     fsfElem.SelectNodes("RelativePath"))
            {
                var outputFullPath = new FileInfo(
                        Path.Combine(
                            extractToDir,
                            relPathElem.InnerText.SafeToString()
                                .Trim("\\/".ToCharArray())))
                    .FullName;

                if (inputByFullPath.TryGetValue(outputFullPath, out var inputFsf))
                {
                    EnsureUnchangedSinceRead(inputFsf, outputFullPath);
                }
            }
        }
    }

    private static bool IsAlwaysOverwriteElement(XmlElement elem)
    {
        bool alwaysOverwrite = false;

        XmlNode aoNode = elem.SelectSingleNode("AlwaysOverwrite");
        if (!ReferenceEquals(aoNode, null) && aoNode.InnerText == "true")
        {
            alwaysOverwrite = true;
        }

        XmlNode omNode = elem.SelectSingleNode("OverwriteMode");
        if (!ReferenceEquals(omNode, null))
        {
            if (string.Equals(omNode.InnerText, "Always", StringComparison.OrdinalIgnoreCase))
            {
                alwaysOverwrite = true;
            }
            else if (string.Equals(omNode.InnerText, "Never", StringComparison.OrdinalIgnoreCase))
            {
                alwaysOverwrite = false;
            }
        }

        return alwaysOverwrite;
    }

    private static void EnsureUnchangedSinceRead(FileSetFile inputFsf, string fullPath)
    {
        if (!ReferenceEquals(inputFsf.ZippedBinaryFileContents, null))
        {
            var originalBytes = inputFsf.GetFileSetFileBinaryContents();
            var currentBytes = File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
            if (currentBytes is null || !originalBytes.SequenceEqual(currentBytes))
            {
                throw new Exception(
                    $"Refusing to overwrite '{fullPath}': it was read as tool input and changed on disk while the tool was running.");
            }

            return;
        }

        var originalText = inputFsf.GetFileSetFileContents();
        var currentText = File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
        if (currentText is null || currentText != originalText)
        {
            throw new Exception(
                $"Refusing to overwrite '{fullPath}': it was read as tool input and changed on disk while the tool was running.");
        }
    }

    public static int SaveFileSet(
        EffortlessProject project,
        string transpilerKey,
        string cwd,
        byte[] zippedOutputFileSet,
        string inputFileSetXml,
        bool skipClean,
        bool debug = false)
    {
        if (!skipClean)
        {
            CleanFileSet(
                project,
                transpilerKey,
                cwd,
                debug,
                deleteEmptyDirs: false);
        }

        var fileSetXml = zippedOutputFileSet.UnzipToString();
        string workingDir = project.GetEffortlessDI().ToString();

        string extractToDir = project?.RootPath ?? workingDir;
        if (project != null)
        {
            var relPath = GetCurrentRelativePath(
                project,
                transpilerKey,
                cwd);
            if (!string.IsNullOrEmpty(relPath))
            {
                extractToDir = Path.Combine(project.RootPath, relPath);
            }
        }

        ValidateSelfSourceOverwrites(project, inputFileSetXml, fileSetXml, extractToDir);

        var tempFI = new FileInfo(
            Path.Combine(
                workingDir,
                string.Format("tempFileSet_{0}.xml", Guid.NewGuid())));
        File.WriteAllText(tempFI.FullName, fileSetXml);

        tempFI.FullName.SplitFileSetFile(extractToDir);
        tempFI.Delete();

        SavePreviousFileSet(
            project,
            transpilerKey,
            cwd,
            fileSetXml,
            debug);

        return 0;
    }

    public static void CleanFileSet(
        EffortlessProject project,
        string transpilerKey,
        string cwd,
        bool debug = false,
        bool deleteEmptyDirs = true)
    {
        if (project is null)
        {
            return;
        }

        FileInfo zfsFI = GetZFSFI(project, transpilerKey, cwd);
        if (zfsFI.Exists)
        {
            var previousFileSet = File.ReadAllBytes(zfsFI.FullName);
            previousFileSet.CleanZippedFileSet(
                debug,
                deleteEmptyDirs);
        }
    }
}
