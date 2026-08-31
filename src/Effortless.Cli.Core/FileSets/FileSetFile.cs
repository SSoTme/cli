using Newtonsoft.Json;

namespace Effortless.Cli.FileSets;

public class FileSetFile
{
    public FileSetFile()
    {
        FileSetFileId = Guid.NewGuid();
    }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "FileSetFileId")]
    public Guid FileSetFileId { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "FileSetId")]
    public Guid FileSetId { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "RelativePath")]
    public string RelativePath { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "FileContents")]
    public string FileContents { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ZippedFileContents")]
    public byte[] ZippedFileContents { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "BinaryFileContents")]
    public byte[] BinaryFileContents { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ZippedTextFileContents")]
    public byte[] ZippedTextFileContents { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ZippedBinaryFileContents")]
    public byte[] ZippedBinaryFileContents { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "AlwaysOverwrite")]
    public bool AlwaysOverwrite { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "OverwriteMode")]
    public string OverwriteMode { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "SkipClean")]
    public bool SkipClean { get; set; }

    public string OriginalRelativePath { get; set; }

    public override string ToString()
    {
        return RelativePath;
    }

    internal void ClearContents()
    {
        BinaryFileContents = null;
        FileContents = null;
        ZippedBinaryFileContents = null;
        ZippedFileContents = null;
        ZippedTextFileContents = null;
    }
}
