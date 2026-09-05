#nullable enable
using System.Net;
using System.Text;
using System.Xml;
using Effortless.Cli.FileSets;

namespace Effortless.Cli.LocalTools;

/// <summary>
/// The fileset half of the script-tool contract: unpacks a request's input
/// fileset into a directory and packs an output directory back into the
/// FileSet XML the CLI already knows how to save.
/// </summary>
internal static class LocalFileSets
{
    public static void WriteInputDirectory(string? inputFileSetXml, string directory)
    {
        Directory.CreateDirectory(directory);
        if (string.IsNullOrWhiteSpace(inputFileSetXml) || !inputFileSetXml.Contains('<'))
        {
            return;
        }

        var xml = inputFileSetXml[inputFileSetXml.IndexOf('<')..]
            .Replace("FileContents><?xml ", "FileContents>&lt;?xml", StringComparison.Ordinal);
        var document = new XmlDocument();
        document.LoadXml(xml);
        foreach (XmlElement element in document.SelectNodes("//FileSetFile")!)
        {
            var relativePath = element.SelectSingleNode("RelativePath")?.InnerText?.Trim('/', '\\');
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var target = ResolveInside(directory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            var text = element.SelectSingleNode("FileContents");
            if (text is not null)
            {
                File.WriteAllText(
                    target,
                    WebUtility.HtmlDecode(text.InnerXml).UnwrapCDATA(),
                    new UTF8Encoding(false));
                continue;
            }

            var zippedText = element.SelectSingleNode("ZippedTextFileContents")
                ?? element.SelectSingleNode("ZippedFileContents");
            if (zippedText is not null)
            {
                File.WriteAllText(
                    target,
                    Convert.FromBase64String(zippedText.InnerXml).UnzipToString(),
                    new UTF8Encoding(false));
                continue;
            }

            var zippedBinary = element.SelectSingleNode("ZippedBinaryFileContents");
            if (zippedBinary is not null)
            {
                File.WriteAllBytes(target, Convert.FromBase64String(zippedBinary.InnerXml).Unzip());
                continue;
            }

            var binary = element.SelectSingleNode("BinaryFileContents");
            if (binary is not null)
            {
                File.WriteAllBytes(target, Convert.FromBase64String(binary.InnerXml));
            }
        }
    }

    /// <summary>
    /// Every file under <paramref name="directory"/> becomes one
    /// AlwaysOverwrite entry: text as FileContents, anything XML cannot carry
    /// verbatim as ZippedBinaryFileContents.
    /// </summary>
    public static string ReadOutputDirectory(string directory)
    {
        var fileSet = new FileSet();
        if (Directory.Exists(directory))
        {
            var root = Path.GetFullPath(directory);
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                var bytes = File.ReadAllBytes(file);
                var entry = new FileSetFile
                {
                    RelativePath = Path.GetRelativePath(root, file).Replace('\\', '/'),
                    AlwaysOverwrite = true,
                };
                if (TryDecodeXmlSafeText(bytes, out var text))
                {
                    entry.FileContents = text;
                }
                else
                {
                    entry.ZippedBinaryFileContents = bytes.Zip();
                }

                fileSet.FileSetFiles.Add(entry);
            }
        }

        return fileSet.ToXml();
    }

    public static byte[] ZipFileSetXml(string fileSetXml) => fileSetXml.Zip();

    private static string ResolveInside(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!full.StartsWith(fullRoot, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Input file path '{relativePath}' escapes the input directory.");
        }

        return full;
    }

    private static bool TryDecodeXmlSafeText(byte[] bytes, out string? text)
    {
        text = null;
        try
        {
            var decoded = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            foreach (var character in decoded)
            {
                if (character < 0x20 && character is not '\t' and not '\n' and not '\r')
                {
                    return false;
                }
            }

            if (decoded.Contains("[$$NEWUUID$$]", StringComparison.Ordinal))
            {
                return false;
            }

            text = decoded;
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
