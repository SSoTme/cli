#nullable enable
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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
    /// The script tool's per-file overwrite declaration, written at the root
    /// of EFFORTLESS_OUTPUT_DIR and never shipped: a JSON object mapping a
    /// relative path or glob to "Always" or "Never", exactly the values the
    /// FileSet OverwriteMode element takes.
    /// </summary>
    public const string OverwriteModesFileName = "effortless-overwrite-modes.json";

    /// <summary>
    /// Every file under <paramref name="directory"/> becomes one FileSet entry:
    /// text as FileContents, anything XML cannot carry verbatim as
    /// ZippedBinaryFileContents. Overwrite semantics are the protocol's, untouched:
    /// a file gets an OverwriteMode element only when the script declared one in
    /// <see cref="OverwriteModesFileName"/>; an undeclared file carries no node
    /// and so is written once and never overwritten, like any other tool's file.
    /// </summary>
    public static string ReadOutputDirectory(string directory)
    {
        var fileSet = new FileSet();
        if (Directory.Exists(directory))
        {
            var root = Path.GetFullPath(directory);
            var modes = OverwriteModes.Load(Path.Combine(root, OverwriteModesFileName));
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                var relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relativePath == OverwriteModesFileName)
                {
                    continue;
                }

                var bytes = File.ReadAllBytes(file);
                var entry = new FileSetFile
                {
                    RelativePath = relativePath,
                    OverwriteMode = modes.For(relativePath),
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

    /// <summary>
    /// The parsed overwrite declaration. Keys are relative paths or globs
    /// (<c>*</c>, <c>**</c>, <c>?</c>); the first matching key in file order wins.
    /// Values are the OverwriteMode strings "Always" and "Never"; anything else
    /// is a tool error, because the CLI must never guess an overwrite mode.
    /// </summary>
    internal sealed class OverwriteModes
    {
        private readonly List<(Regex Pattern, string Mode)> _rules = new();

        public static OverwriteModes Load(string path)
        {
            var modes = new OverwriteModes();
            if (!File.Exists(path))
            {
                return modes;
            }

            Newtonsoft.Json.Linq.JObject document;
            try
            {
                document = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
            }
            catch (Newtonsoft.Json.JsonException exception)
            {
                throw new InvalidDataException(
                    $"{OverwriteModesFileName} is not valid JSON: {exception.Message}");
            }

            foreach (var property in document.Properties())
            {
                var mode = property.Value is Newtonsoft.Json.Linq.JValue { Type: Newtonsoft.Json.Linq.JTokenType.String } value
                    ? (string?)value.Value
                    : null;
                if (!string.Equals(mode, "Always", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(mode, "Never", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"{OverwriteModesFileName}: '{property.Name}' must be \"Always\" or \"Never\", not '{property.Value}'.");
                }

                modes._rules.Add((GlobToRegex(property.Name), mode!.Length == 6 ? "Always" : "Never"));
            }

            return modes;
        }

        public string? For(string relativePath)
        {
            foreach (var (pattern, mode) in _rules)
            {
                if (pattern.IsMatch(relativePath))
                {
                    return mode;
                }
            }

            return null;
        }

        private static Regex GlobToRegex(string glob)
        {
            var normalized = glob.Replace('\\', '/').TrimStart('/');
            var builder = new StringBuilder("^");
            for (var i = 0; i < normalized.Length; i++)
            {
                var c = normalized[i];
                if (c == '*')
                {
                    if (i + 1 < normalized.Length && normalized[i + 1] == '*')
                    {
                        builder.Append(".*");
                        i++;
                        if (i + 1 < normalized.Length && normalized[i + 1] == '/')
                        {
                            i++;
                        }
                    }
                    else
                    {
                        builder.Append("[^/]*");
                    }
                }
                else if (c == '?')
                {
                    builder.Append("[^/]");
                }
                else
                {
                    builder.Append(Regex.Escape(c.ToString()));
                }
            }

            builder.Append('$');
            return new Regex(builder.ToString(), RegexOptions.CultureInvariant);
        }
    }
}
