using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Effortless.Cli.E2E.Harness;

internal enum FileSetContentKind
{
    FileContents,
    ZippedFileContents,
    ZippedTextFileContents,
    BinaryFileContents,
    ZippedBinaryFileContents,
    NoContent,
}

internal sealed record FileSetEntry(
    string RelativePath,
    FileSetContentKind Kind,
    byte[] Contents,
    bool? AlwaysOverwrite = null,
    string? OverwriteMode = null,
    bool? SkipClean = null,
    string? OriginalRelativePath = null)
{
    public string Text => Encoding.UTF8.GetString(Contents);

    public static FileSetEntry TextFile(
        string path,
        string contents,
        bool? alwaysOverwrite = null,
        string? overwriteMode = null,
        bool? skipClean = null) =>
        new(
            path,
            FileSetContentKind.FileContents,
            Encoding.UTF8.GetBytes(contents),
            alwaysOverwrite,
            overwriteMode,
            skipClean);

    public static FileSetEntry ZippedTextFile(
        string path,
        string contents,
        bool? alwaysOverwrite = null,
        string? overwriteMode = null,
        bool? skipClean = null,
        bool legacyElementName = false) =>
        new(
            path,
            legacyElementName
                ? FileSetContentKind.ZippedFileContents
                : FileSetContentKind.ZippedTextFileContents,
            Encoding.UTF8.GetBytes(contents),
            alwaysOverwrite,
            overwriteMode,
            skipClean);

    public static FileSetEntry BinaryFile(
        string path,
        byte[] contents,
        bool? alwaysOverwrite = null,
        string? overwriteMode = null,
        bool? skipClean = null,
        bool zipped = false) =>
        new(
            path,
            zipped
                ? FileSetContentKind.ZippedBinaryFileContents
                : FileSetContentKind.BinaryFileContents,
            contents,
            alwaysOverwrite,
            overwriteMode,
            skipClean);

    public static FileSetEntry NoContent(string path) =>
        new(path, FileSetContentKind.NoContent, []);
}

internal static class FileSetXml
{
    public static string Build(IEnumerable<FileSetEntry> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            OmitXmlDeclaration = false,
        };
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("FileSet");
            writer.WriteElementString("FileSetId", Guid.Empty.ToString());
            writer.WriteStartElement("FileSetFiles");
            foreach (var file in files)
            {
                WriteFile(writer, file);
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static IReadOnlyList<FileSetEntry> Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new InvalidDataException("FileSet XML is empty.");
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(TrimToXml(xml), LoadOptions.PreserveWhitespace);
        }
        catch (Exception exception) when (exception is XmlException or ArgumentException)
        {
            throw new InvalidDataException("FileSet XML is malformed.", exception);
        }

        if (document.Root?.Name.LocalName != "FileSet")
        {
            throw new InvalidDataException("FileSet XML must have a <FileSet> root.");
        }

        var result = new List<FileSetEntry>();
        foreach (var element in document.Descendants().Where(node => node.Name.LocalName == "FileSetFile"))
        {
            var relativePath = Child(element, "RelativePath")?.Value;
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new InvalidDataException("A FileSetFile is missing RelativePath.");
            }

            var (kind, bytes) = ReadContents(element);
            result.Add(
                new FileSetEntry(
                    relativePath,
                    kind,
                    bytes,
                    ParseNullableBool(Child(element, "AlwaysOverwrite")),
                    Child(element, "OverwriteMode")?.Value,
                    ParseNullableBool(Child(element, "SkipClean")),
                    Child(element, "OriginalRelativePath")?.Value));
        }

        return result;
    }

    public static byte[] Gzip(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(bytes);
        }

        return output.ToArray();
    }

    public static byte[] Gunzip(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            using var input = new MemoryStream(bytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException("The FileSet gzip payload is invalid.", exception);
        }
    }

    public static string GzipToBase64(string value) =>
        Convert.ToBase64String(Gzip(Encoding.UTF8.GetBytes(value)));

    public static string GunzipBase64ToString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException("The FileSet base64 payload is empty.");
        }

        try
        {
            return Encoding.UTF8.GetString(Gunzip(Convert.FromBase64String(value)));
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The FileSet payload is not valid base64.", exception);
        }
    }

    private static void WriteFile(XmlWriter writer, FileSetEntry file)
    {
        if (string.IsNullOrWhiteSpace(file.RelativePath))
        {
            throw new ArgumentException("FileSet paths cannot be empty.", nameof(file));
        }

        writer.WriteStartElement("FileSetFile");
        writer.WriteElementString("RelativePath", file.RelativePath);
        if (!string.IsNullOrEmpty(file.OriginalRelativePath))
        {
            writer.WriteElementString("OriginalRelativePath", file.OriginalRelativePath);
        }

        switch (file.Kind)
        {
            case FileSetContentKind.FileContents:
                writer.WriteElementString("FileContents", file.Text);
                break;
            case FileSetContentKind.ZippedFileContents:
            case FileSetContentKind.ZippedTextFileContents:
                writer.WriteElementString(
                    file.Kind.ToString(),
                    Convert.ToBase64String(Gzip(file.Contents)));
                break;
            case FileSetContentKind.BinaryFileContents:
                writer.WriteElementString("BinaryFileContents", Convert.ToBase64String(file.Contents));
                break;
            case FileSetContentKind.ZippedBinaryFileContents:
                writer.WriteElementString(
                    "ZippedBinaryFileContents",
                    Convert.ToBase64String(Gzip(file.Contents)));
                break;
            case FileSetContentKind.NoContent:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(file), file.Kind, "Unknown FileSet content kind.");
        }

        if (file.AlwaysOverwrite is not null)
        {
            writer.WriteElementString(
                "AlwaysOverwrite",
                XmlConvert.ToString(file.AlwaysOverwrite.Value));
        }

        if (file.OverwriteMode is not null)
        {
            writer.WriteElementString("OverwriteMode", file.OverwriteMode);
        }

        if (file.SkipClean is not null)
        {
            writer.WriteElementString("SkipClean", XmlConvert.ToString(file.SkipClean.Value));
        }

        writer.WriteEndElement();
    }

    private static (FileSetContentKind Kind, byte[] Bytes) ReadContents(XElement element)
    {
        foreach (var kind in Enum.GetValues<FileSetContentKind>())
        {
            if (kind == FileSetContentKind.NoContent)
            {
                continue;
            }

            var content = Child(element, kind.ToString());
            if (content is null)
            {
                continue;
            }

            try
            {
                return kind switch
                {
                    FileSetContentKind.FileContents => (kind, Encoding.UTF8.GetBytes(content.Value)),
                    FileSetContentKind.BinaryFileContents => (kind, Convert.FromBase64String(content.Value)),
                    _ => (kind, Gunzip(Convert.FromBase64String(content.Value))),
                };
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException(
                    $"{kind} for '{Child(element, "RelativePath")?.Value}' is not valid base64.",
                    exception);
            }
        }

        return (FileSetContentKind.NoContent, []);
    }

    private static XElement? Child(XElement element, string name) =>
        element.Elements().FirstOrDefault(child => child.Name.LocalName == name);

    private static bool? ParseNullableBool(XElement? element) =>
        element is null ? null : XmlConvert.ToBoolean(element.Value.ToLowerInvariant());

    private static string TrimToXml(string value)
    {
        var start = value.IndexOf('<');
        if (start < 0)
        {
            throw new InvalidDataException("FileSet XML contains no XML element.");
        }

        return value[start..].TrimStart('\uFEFF');
    }
}
