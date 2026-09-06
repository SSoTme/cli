using System.ComponentModel;
using Newtonsoft.Json;

namespace Effortless.Cli.FileSets;

public class FileSet
{
    public FileSet()
    {
        FileSetId = Guid.NewGuid();
        FileSetFiles = new BindingList<FileSetFile>();
    }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "FileSetId")]
    public Guid FileSetId { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "CreatedOn")]
    public DateTime? CreatedOn { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "FileSetFiles")]
    public BindingList<FileSetFile> FileSetFiles { get; set; }
}
