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
        var ssotmeDI = new DirectoryInfo(
            string.Format("{0}/.ssotme", project.RootPath));
        if (!ssotmeDI.Exists)
        {
            ssotmeDI.Create();
            ssotmeDI.Attributes =
                FileAttributes.Directory | FileAttributes.Hidden;
        }

        var zfsFileName = string.Format(
            "{0}/{1}/{2}.zfs",
            ssotmeDI.FullName,
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

    public static string RemoveSelfSourceEntries(
        EffortlessProject project,
        string transpilerKey,
        string cwd,
        string fileSetXml,
        string inputFileSetXml,
        string extractToDir)
    {
        if (project is null || string.IsNullOrEmpty(inputFileSetXml))
        {
            return fileSetXml;
        }

        if (string.IsNullOrEmpty(fileSetXml) || !fileSetXml.Contains("<"))
        {
            return fileSetXml;
        }

        HashSet<string> inputFullPaths;
        try
        {
            inputFullPaths = inputFileSetXml
                .ToFileSet()
                .FileSetFiles
                .Select(fsf => fsf.OriginalRelativePath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(
                    p => new FileInfo(
                            Path.Combine(
                                project.RootPath,
                                p.Trim("\\/".ToCharArray())))
                        .FullName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return fileSetXml;
        }

        if (!inputFullPaths.Any())
        {
            return fileSetXml;
        }

        var doc = new XmlDocument();
        var trimmedXml = fileSetXml.Substring(fileSetXml.IndexOf("<"));
        doc.LoadXml(trimmedXml);
        if (doc.DocumentElement is null ||
            doc.DocumentElement.Name != "FileSet")
        {
            return fileSetXml;
        }

        var toRemove = new List<XmlElement>();
        foreach (XmlElement fsfElem in
                 doc.DocumentElement.SelectNodes("//FileSetFile"))
        {
            foreach (XmlElement relPathElem in
                     fsfElem.SelectNodes("RelativePath"))
            {
                var outputFullPath = new FileInfo(
                        Path.Combine(
                            extractToDir,
                            relPathElem.InnerText.SafeToString()
                                .Trim("\\/".ToCharArray())))
                    .FullName;
                if (inputFullPaths.Contains(outputFullPath))
                {
                    toRemove.Add(fsfElem);
                    break;
                }
            }
        }

        if (!toRemove.Any())
        {
            return fileSetXml;
        }

        foreach (var fsfElem in toRemove)
        {
            fsfElem.ParentNode.RemoveChild(fsfElem);
        }

        return doc.OuterXml;
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
        string workingDir = project.GetSSoTmeDI().ToString();

        var tempFI = new FileInfo(
            Path.Combine(
                workingDir,
                string.Format("tempFileSet_{0}.xml", Guid.NewGuid())));
        File.WriteAllText(tempFI.FullName, fileSetXml);

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
