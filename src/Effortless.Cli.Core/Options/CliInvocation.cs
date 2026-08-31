using Effortless.Cli.FileSets;
using Effortless.Cli.Project;

namespace Effortless.Cli.Options;

public class CliInvocation
{
    public CliInvocation()
    {
        Options = new CliOptions();
        RemainingArguments = new List<string>();
        Transpiler = string.Empty;
        Account = string.Empty;
        ErrorText = string.Empty;
        UsageHeader = string.Empty;
        UsageOptions = string.Empty;
        CurrentDirectory = GetCurrentDirectory();
    }

    public CliOptions Options { get; set; }

    public List<string> RemainingArguments { get; set; }

    public string RawTranspilerArg { get; set; }

    public string TargetUrl { get; set; }

    public string Transpiler { get; set; }

    public string Account { get; set; }

    public string ResolvedVersionKey { get; set; }

    public string ResolvedVersionUrl { get; set; }

    public string ResolvedVersionLabel { get; set; }

    public string ResolvedToolName { get; set; }

    public EffortlessProject Project { get; set; }

    public FileSet InputFileSet { get; set; }

    public string InputFileSetXml { get; set; }

    public string Jwt { get; set; }

    public bool SuppressTranspile { get; set; }

    public int ParseResult { get; set; }

    public bool IsBuildOperation { get; set; }

    public bool SuppressVersionLabel { get; set; }

    public bool SkipRemoteToolsLookup { get; set; }

    public bool SuppressTranspilerErrorOutput { get; set; }

    public string CurrentDirectory { get; set; }

    public bool ContinueOnError { get; set; }

    public bool UpdateProject { get; set; }

    public bool HasExplicitVersionError { get; set; }

    public bool HasErrors { get; set; }

    public string ErrorText { get; set; }

    public string UsageHeader { get; set; }

    public string UsageOptions { get; set; }

    private static string GetCurrentDirectory()
    {
        try
        {
            return Environment.CurrentDirectory;
        }
        catch
        {
            return null;
        }
    }
}
