using System.ComponentModel;
using System.Text;
using Effortless.Cli.FileSets;

namespace Effortless.Cli.Tests;

public sealed class FileSetTests
{
    [Fact(DisplayName = "unit-fileset-xml-roundtrip: FileSet XML round-trips every content kind")]
    public void FileSetXmlRoundTripsEveryContentKind()
    {
        var source = new FileSet
        {
            CreatedOn = new DateTime(2026, 8, 30, 12, 34, 56, DateTimeKind.Utc),
            FileSetFiles = new BindingList<FileSetFile>
            {
                new()
                {
                    RelativePath = "plain.txt",
                    OriginalRelativePath = "input/plain.txt",
                    FileContents = "plain",
                    AlwaysOverwrite = true,
                    OverwriteMode = "Always",
                    SkipClean = true,
                },
                new() { RelativePath = "legacy.txt", ZippedFileContents = GZip.Zip("legacy") },
                new() { RelativePath = "binary.bin", BinaryFileContents = [0, 1, 255] },
                new() { RelativePath = "text.txt", ZippedTextFileContents = GZip.Zip("text") },
                new() { RelativePath = "zipped.bin", ZippedBinaryFileContents = GZip.Zip(new byte[] { 255, 1, 0 }) },
                new() { RelativePath = "empty.txt" },
            },
        };

        var xml = FileSetXml.ToXml(source);
        var actual = FileSetXml.ToFileSet(xml);

        Assert.Contains("<FileSetFiles>", xml, StringComparison.Ordinal);
        Assert.Contains("<ZippedFileContents>", xml, StringComparison.Ordinal);
        Assert.Contains("<ZippedTextFileContents>", xml, StringComparison.Ordinal);
        Assert.Contains("<BinaryFileContents>", xml, StringComparison.Ordinal);
        Assert.Contains("<ZippedBinaryFileContents>", xml, StringComparison.Ordinal);
        Assert.Equal(source.FileSetId, actual.FileSetId);
        Assert.Equal(source.CreatedOn, actual.CreatedOn);
        Assert.Equal(source.FileSetFiles.Count, actual.FileSetFiles.Count);

        for (var index = 0; index < source.FileSetFiles.Count; index++)
        {
            var expected = source.FileSetFiles[index];
            var observed = actual.FileSetFiles[index];
            Assert.Equal(expected.RelativePath, observed.RelativePath);
            Assert.Equal(expected.OriginalRelativePath, observed.OriginalRelativePath);
            Assert.Equal(expected.FileContents, observed.FileContents);
            Assert.Equal(expected.ZippedFileContents, observed.ZippedFileContents);
            Assert.Equal(expected.BinaryFileContents, observed.BinaryFileContents);
            Assert.Equal(expected.ZippedTextFileContents, observed.ZippedTextFileContents);
            Assert.Equal(expected.ZippedBinaryFileContents, observed.ZippedBinaryFileContents);
            Assert.Equal(expected.AlwaysOverwrite, observed.AlwaysOverwrite);
            Assert.Equal(expected.OverwriteMode, observed.OverwriteMode);
            Assert.Equal(expected.SkipClean, observed.SkipClean);
        }
    }

    [Fact(DisplayName = "unit-split-fileset-rules: FileSet writer honors overwrite and content rules")]
    public void FileSetWriterHonorsOverwriteAndContentRules()
    {
        using var directory = new TestDirectory();
        File.WriteAllText(directory.File("never.txt"), "local edits");
        File.WriteAllText(directory.File("always-mode.txt"), "old");

        var files = new FileSet
        {
            FileSetFiles = new BindingList<FileSetFile>
            {
                new() { RelativePath = "plain.txt", FileContents = "plain", AlwaysOverwrite = true },
                new() { RelativePath = "never.txt", FileContents = "server", OverwriteMode = "Never" },
                new() { RelativePath = "zipped.txt", ZippedTextFileContents = GZip.Zip("zipped"), AlwaysOverwrite = true },
                new() { RelativePath = "zipped.bin", ZippedBinaryFileContents = GZip.Zip(new byte[] { 0, 1, 255 }), AlwaysOverwrite = true },
                new() { RelativePath = "always-mode.txt", FileContents = "new", OverwriteMode = "Always" },
                new() { RelativePath = "binary.bin", BinaryFileContents = [255, 1, 0], AlwaysOverwrite = true },
            },
        };

        FileSetWriter.SplitFileSetXml(FileSetXml.ToXml(files), overwriteAll: false, directory.Path);

        Assert.Equal("plain", File.ReadAllText(directory.File("plain.txt")));
        Assert.Equal("local edits", File.ReadAllText(directory.File("never.txt")));
        Assert.Equal("zipped" + Environment.NewLine, File.ReadAllText(directory.File("zipped.txt")));
        Assert.Equal(new byte[] { 0, 1, 255 }, File.ReadAllBytes(directory.File("zipped.bin")));
        Assert.Equal("new", File.ReadAllText(directory.File("always-mode.txt")));
        Assert.Equal(new byte[] { 255, 1, 0 }, File.ReadAllBytes(directory.File("binary.bin")));

        var invalid = new FileSet
        {
            FileSetFiles = new BindingList<FileSetFile>
            {
                new() { RelativePath = "missing.txt" },
            },
        };
        var exception = Assert.Throws<Exception>(
            () => FileSetWriter.SplitFileSetXml(FileSetXml.ToXml(invalid), overwriteAll: false, directory.Path));
        Assert.Contains("without content nodes", exception.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "unit-clean-fileset-rules: FileSet cleaner only removes Always entries")]
    public void FileSetCleanerOnlyRemovesAlwaysEntries()
    {
        using var directory = new TestDirectory();
        Directory.CreateDirectory(directory.File("delete"));
        Directory.CreateDirectory(directory.File("keep"));
        Directory.CreateDirectory(directory.File("skip"));
        File.WriteAllText(directory.File("delete/always.txt"), "generated");
        File.WriteAllText(directory.File("keep/never.txt"), "edited");
        File.WriteAllText(directory.File("skip/skip.txt"), "generated");

        var ledger = new FileSet
        {
            FileSetFiles = new BindingList<FileSetFile>
            {
                new() { RelativePath = "delete/always.txt", FileContents = "generated", AlwaysOverwrite = true },
                new() { RelativePath = "keep/never.txt", FileContents = "generated", OverwriteMode = "Never" },
                new()
                {
                    RelativePath = "skip/skip.txt",
                    FileContents = "generated",
                    AlwaysOverwrite = true,
                    SkipClean = true,
                },
            },
        };

        var original = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = directory.Path;
            FileSetCleaner.CleanFileSet(FileSetXml.ToXml(ledger), debug: false, deleteEmptyDirs: true);
        }
        finally
        {
            Environment.CurrentDirectory = original;
        }

        Assert.False(File.Exists(directory.File("delete/always.txt")));
        Assert.False(Directory.Exists(directory.File("delete")));
        Assert.Equal("edited", File.ReadAllText(directory.File("keep/never.txt")));
        Assert.Equal("generated", File.ReadAllText(directory.File("skip/skip.txt")));
    }
}
