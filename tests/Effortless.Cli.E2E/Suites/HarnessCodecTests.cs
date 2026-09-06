using System.Text;
using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

public sealed class HarnessCodecTests
{
    [Fact(DisplayName = "codec-fileset-roundtrip: all legacy content elements round-trip")]
    public void AllLegacyContentElementsRoundTrip()
    {
        FileSetEntry[] expected =
        [
            FileSetEntry.TextFile("plain.txt", "<root>& text</root>", overwriteMode: "Always"),
            FileSetEntry.ZippedTextFile("modern.txt", "modern", alwaysOverwrite: true),
            FileSetEntry.ZippedTextFile(
                "legacy.txt",
                "legacy",
                overwriteMode: "Never",
                legacyElementName: true),
            FileSetEntry.BinaryFile("raw.bin", [0, 1, 2, 255]),
            FileSetEntry.BinaryFile("zipped.bin", [255, 2, 1, 0], zipped: true),
            FileSetEntry.NoContent("marker.txt"),
        ];

        var xml = FileSetXml.Build(expected);
        var actual = FileSetXml.Parse(xml);

        Assert.Equal(expected.Length, actual.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].RelativePath, actual[index].RelativePath);
            Assert.Equal(expected[index].Kind, actual[index].Kind);
            Assert.Equal(expected[index].Contents, actual[index].Contents);
            Assert.Equal(expected[index].AlwaysOverwrite, actual[index].AlwaysOverwrite);
            Assert.Equal(expected[index].OverwriteMode, actual[index].OverwriteMode);
        }
    }

    [Fact(DisplayName = "codec-fileset-gzip: XML gzip/base64 round-trips")]
    public void GzipBase64RoundTrips()
    {
        const string xml = "<FileSet><FileSetFiles /></FileSet>";

        var encoded = FileSetXml.GzipToBase64(xml);

        Assert.Equal(xml, FileSetXml.GunzipBase64ToString(encoded));
    }

    [Theory(DisplayName = "codec-fileset-invalid: invalid wire data fails explicitly")]
    [InlineData("")]
    [InlineData("not base64")]
    [InlineData("bm90IGd6aXA=")]
    public void InvalidCompressedDataFailsExplicitly(string value)
    {
        Assert.Throws<InvalidDataException>(() => FileSetXml.GunzipBase64ToString(value));
    }

    [Fact(DisplayName = "codec-fileset-utf8: zipped text preserves UTF-8")]
    public void ZippedTextPreservesUtf8()
    {
        const string value = "héllø 世界";
        var bytes = FileSetXml.Gzip(Encoding.UTF8.GetBytes(value));

        Assert.Equal(value, Encoding.UTF8.GetString(FileSetXml.Gunzip(bytes)));
    }
}
