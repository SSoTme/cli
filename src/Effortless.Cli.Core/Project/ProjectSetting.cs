using Newtonsoft.Json;

namespace Effortless.Cli.Project;

public class ProjectSetting
{
    public ProjectSetting()
    {
        ProjectSettingId = Guid.NewGuid();
    }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ProjectSettingId")]
    public Guid ProjectSettingId { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "SSoTmeProjectId")]
    public Guid SSoTmeProjectId { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "Name")]
    public string Name { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "Value")]
    public string Value { get; set; }

    public override string ToString()
    {
        return string.Format("ProjectSetting: {0}", Name);
    }
}
