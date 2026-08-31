using System.ComponentModel;
using Effortless.Cli.Config;
using Effortless.Cli.Project;
using Newtonsoft.Json;

namespace Effortless.Cli;

public class TranspilePayload
{
    public TranspilePayload()
    {
        PayloadId = Guid.NewGuid().ToString();
        Settings = new Dictionary<string, string>();
        CLIParams = Array.Empty<string>();
    }

    [JsonProperty(PropertyName = "payloadId")]
    public string PayloadId { get; set; }

    [JsonProperty(PropertyName = "senderId")]
    public string SenderId { get; set; }

    [JsonProperty(PropertyName = "senderName")]
    public string SenderName { get; set; }

    [JsonProperty(PropertyName = "settings")]
    public Dictionary<string, string> Settings { get; set; }

    [JsonProperty(PropertyName = "transpiler")]
    public Transpiler Transpiler { get; set; }

    [JsonProperty(PropertyName = "transpileRequest")]
    public TranspileRequest TranspileRequest { get; set; }

    [JsonProperty(PropertyName = "cliAccount")]
    public string CLIAccount { get; set; }

    [JsonProperty(PropertyName = "cliInput")]
    public string[] CLIInput { get; set; }

    [JsonProperty(PropertyName = "cliInputFileContents")]
    public string CLIInputFileContents { get; set; }

    [JsonProperty(PropertyName = "cliInputFileSetJson")]
    public string CLIInputFileSetJson { get; set; }

    [JsonProperty(PropertyName = "cliInputFileSetXml")]
    public string CLIInputFileSetXml { get; set; }

    [JsonProperty(PropertyName = "cliOutput")]
    public string CLIOutput { get; set; }

    [JsonProperty(PropertyName = "cliParams")]
    public string[] CLIParams { get; set; }

    [JsonProperty(PropertyName = "cliTranspiler")]
    public string CLITranspiler { get; set; }

    [JsonProperty(PropertyName = "cliWaitTimeout")]
    public int CLIWaitTimeout { get; set; }

    [JsonProperty(PropertyName = "cliDebug")]
    public bool CLIDebug { get; set; }

    [JsonProperty(PropertyName = "cliJwt")]
    public string CLIJwt { get; set; }

    [JsonProperty(PropertyName = "Logs")]
    public List<LogEntry> Logs { get; set; }

    [JsonProperty(PropertyName = "TaskId")]
    public string TaskId { get; set; }

    [JsonProperty(PropertyName = "TaskStatus")]
    public string TaskStatus { get; set; }

    [JsonProperty(PropertyName = "Exception")]
    public Exception Exception { get; set; }

    [JsonProperty(PropertyName = "ErrorMessage")]
    public string ErrorMessage { get; set; }

    [JsonIgnore]
    public EffortlessProject SSoTmeProject { get; set; }

    [JsonIgnore]
    public KeyFile SSoTmeKey { get; set; }

    public string GetParameterByIndex(int parameterIndex)
    {
        if (!CLIParams.Skip(parameterIndex).Any())
        {
            throw new Exception(string.Format("Parameter {0} not found.", parameterIndex));
        }

        var param = CLIParams.Skip(parameterIndex).First();
        return param.Substring(param.IndexOf("=") + 1);
    }

    public bool HasParamNamed(string paramName)
    {
        return CLIParams.Any(
            anyParam => anyParam.StartsWith(string.Format("{0}=", paramName)));
    }

    public string GetParameterByName(string paramName)
    {
        if (!HasParamNamed(paramName))
        {
            throw new Exception(string.Format("Parameter {0} not found.", paramName));
        }

        var param = CLIParams.First(
            firstParam => firstParam.StartsWith(string.Format("{0}=", paramName)));
        return param.Substring(param.IndexOf("=") + 1);
    }

    public string GetSetting(string settingName, string defaultStringValue)
    {
        if (!Settings.ContainsKey(settingName) ||
            string.IsNullOrEmpty(Settings[settingName]))
        {
            return defaultStringValue;
        }

        return Settings[settingName];
    }
}

public class Transpiler
{
    public Transpiler()
    {
        TranspilerId = Guid.NewGuid();
        TranspileRequests = new BindingList<TranspileRequest>();
        TranspilerInstances = new BindingList<object>();
        TranspilerVersions = new BindingList<object>();
    }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "transpilerId")]
    public Guid TranspilerId { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "accountHolderId")]
    public Guid AccountHolderId { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "transpilerPlatformId")]
    public Guid? TranspilerPlatformId { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "name")]
    public string Name { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "displayName")]
    public string DisplayName { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "description")]
    public string Description { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "createdOn")]
    public DateTime? CreatedOn { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "isActive")]
    public bool IsActive { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "currentRoutingKey")]
    public string CurrentRoutingKey { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "isPrivate")]
    public bool IsPrivate { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "lowerName")]
    public string LowerName { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "upperName")]
    public string UpperName { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "lowerHyphenName")]
    public string LowerHyphenName { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "readMeMD")]
    public string ReadMeMD { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "inputDescriptionMD")]
    public string InputDescriptionMD { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "outputDescriptionMD")]
    public string OutputDescriptionMD { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "exampleMD")]
    public string ExampleMD { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "inputFileTypeId")]
    public Guid? InputFileTypeId { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "outputFileTypeId")]
    public Guid? OutputFileTypeId { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "usageCount")]
    public int UsageCount { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "category")]
    public string Category { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "isRecommended")]
    public bool IsRecommended { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "transpileRequests")]
    public BindingList<TranspileRequest> TranspileRequests { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "transpilerInstances")]
    public BindingList<object> TranspilerInstances { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "transpilerVersions")]
    public BindingList<object> TranspilerVersions { get; set; }

    public override string ToString()
    {
        return DisplayName;
    }
}

public class TranspileRequest
{
    public TranspileRequest()
    {
        TranspileRequestId = Guid.NewGuid();
        LastTranspilerRequestId_ProjectTranspilers = new BindingList<object>();
        TranspileInputFiles = new BindingList<object>();
        TranspileOutputFiles = new BindingList<object>();
    }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "transpileRequestId")]
    public Guid TranspileRequestId { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "transpileRequestStatusId")]
    public Guid TranspileRequestStatusId { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "createdOn")]
    public DateTime CreatedOn { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "transpilerId")]
    public Guid TranspilerId { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "accountHolderId")]
    public Guid AccountHolderId { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "zippedInputFileSet")]
    public byte[] ZippedInputFileSet { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "zippedOutputFileSet")]
    public byte[] ZippedOutputFileSet { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "transpilerInstanceId")]
    public Guid? TranspilerInstanceId { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "lastTranspilerRequestId_ProjectTranspilers")]
    public BindingList<object> LastTranspilerRequestId_ProjectTranspilers { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "transpileInputFiles")]
    public BindingList<object> TranspileInputFiles { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "transpileOutputFiles")]
    public BindingList<object> TranspileOutputFiles { get; set; }

    [JsonProperty(PropertyName = "jsonOutputFileSet")]
    public string JsonOutputFileSet { get; set; }
}
