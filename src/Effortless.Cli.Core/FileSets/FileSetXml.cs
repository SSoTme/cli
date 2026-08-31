using System.Net;
using System.Xml;
using System.Xml.Serialization;
using Effortless.Cli.Text;

namespace Effortless.Cli.FileSets;

public static class FileSetXml
{
    public static FileSet ToFileSet(this string fileSetXml)
    {
        fileSetXml = fileSetXml.Substring(fileSetXml.IndexOf("<"));
        var serializer = new XmlSerializer(typeof(FileSet));
        using var reader = new StringReader(fileSetXml);
        return (FileSet)serializer.Deserialize(reader);
    }

    public static string ToXml(this FileSet fileSet)
    {
        var ser = new XmlSerializer(typeof(FileSet), new XmlAttributeOverrides());
        var ms = new MemoryStream();
        ser.Serialize(ms, fileSet);
        ms.Position = 0;
        var fileSetXml = new StreamReader(ms).ReadToEnd();
        return fileSetXml;
    }

    public static List<XmlElement> FileSetFilesFromFileSetXml(this string fileSetXml)
    {
        var xdoc = new XmlDocument();
        xdoc.LoadXml(fileSetXml);
        return xdoc.DocumentElement
            .SelectNodes("//FileSetFile")
            .OfType<XmlElement>()
            .ToList();
    }

    public static string GetFileContents(this XmlElement fileSetFileElement)
    {
        var fileContentsElement = fileSetFileElement.SelectSingleNode(".//FileContents");
        if (!ReferenceEquals(fileContentsElement, null))
        {
            var fileContents = fileContentsElement.InnerXml;
            return UnwrapCDATA(fileContents);
        }

        var zippedFileContentsElement = fileSetFileElement.SelectSingleNode(".//ZippedTextFileContents");
        if (!ReferenceEquals(zippedFileContentsElement, null))
        {
            var fileContents = Convert.FromBase64String(zippedFileContentsElement.InnerXml).UnzipToString();
            return fileContents;
        }

        return string.Empty;
    }

    public static string ToSingleTextFileFileSetXml(this string inputString, string fileName)
    {
        if (string.IsNullOrEmpty(inputString))
        {
            inputString = string.Empty;
        }

        var xdoc = new XmlDocument();
        var fileSet = xdoc.CreateElement("FileSet");
        xdoc.AppendChild(fileSet);
        var fileSetFiles = xdoc.CreateElement("FileSetFiles");
        fileSet.AppendChild(fileSetFiles);
        var fileSetFile = xdoc.CreateElement("FileSetFile");
        fileSetFiles.AppendChild(fileSetFile);
        var relativePath = xdoc.CreateElement("RelativePath");
        relativePath.InnerText = fileName;
        fileSetFile.AppendChild(relativePath);
        var fileContents = xdoc.CreateElement("ZippedTextFileContents");
        fileContents.InnerXml = Convert.ToBase64String(inputString.Zip());
        fileSetFile.AppendChild(fileContents);
        return xdoc.OuterXml;
    }

    public static string UnwrapCDATA(this string cdataText)
    {
        cdataText = cdataText.SafeToString();
        if (cdataText.StartsWith("<![CDATA[") && cdataText.EndsWith("]]>"))
        {
            var startPosition = "<![CDATA[".Length;
            var lengthToExclude = "<![CDATA[]]>".Length;
            cdataText = cdataText.Substring(startPosition, cdataText.Length - lengthToExclude);
        }

        return cdataText;
    }

    public static string GetFileSetFileContents(this FileSetFile fileSetFile)
    {
        if (ReferenceEquals(fileSetFile, null))
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(fileSetFile.FileContents))
        {
            return fileSetFile.FileContents;
        }

        if (!ReferenceEquals(fileSetFile.ZippedFileContents, null))
        {
            return fileSetFile.ZippedFileContents.UnzipToString();
        }

        return string.Empty;
    }

    public static byte[] GetFileSetFileBinaryContents(this FileSetFile fileSetFile)
    {
        if (!ReferenceEquals(fileSetFile.ZippedBinaryFileContents, null))
        {
            return fileSetFile.ZippedBinaryFileContents.Unzip();
        }

        return Array.Empty<byte>();
    }
}
