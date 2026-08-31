using Plossum.CommandLine;

namespace Effortless.Cli.Options;

[CommandLineManager(
    ApplicationName = "SSoTme CLI",
    Copyright = "Copyright 2026, EffortlessAPI.com",
    Description = @"-p description=\n\nSYNTAX: ssotme {command} [...{additional_args}] [options]\nOptions",
    EnabledOptionStyles =
        OptionStyles.Windows |
        OptionStyles.Unix |
        OptionStyles.File)]
public class CliOptions
{
    public CliOptions()
    {
        input = new List<string>();
        parameters = new List<string>();
        addSetting = new List<string>();
        removeSetting = new List<string>();
        waitTimeout = 180000;
    }

    [CommandLineOption(Description = "Show help about how to use the SSoT.me CLI", MinOccurs = 0, Aliases = "h")]
    public bool help { get; set; }

    [CommandLineOption(Description = "Initialize the current folder as the root of an SSoT.me project. An Optional parameter of force will create a sub-project.", MinOccurs = 0, Aliases = "")]
    public bool init { get; set; }

    [CommandLineOption(Description = "Saves the current command into the SSoT.me Project file", MinOccurs = 0, Aliases = "")]
    public bool install { get; set; }

    [CommandLineOption(Description = "Removes the current command from the SSoT.me Project file", MinOccurs = 0, Aliases = "")]
    public bool uninstall { get; set; }

    [CommandLineOption(Description = "Build any transpilers in the current folder (or children).", MinOccurs = 0, Aliases = "b,replay,rebuild,pull")]
    public bool build { get; set; }

    [CommandLineOption(Description = "Builds all transpilers in the project", MinOccurs = 0, Aliases = "ba,replayall,rebuildAll,pullAll")]
    public bool buildAll { get; set; }

    [CommandLineOption(Description = "Build only the transpilers installed in the current folder", MinOccurs = 0, Aliases = "bl,replaylocal,rebuildLocal,pullLocal")]
    public bool buildLocal { get; set; }

    [CommandLineOption(Description = "Show debug output", MinOccurs = 0, Aliases = "")]
    public bool debug { get; set; }

    [CommandLineOption(Description = "Describes the current SSoT.me Project (and all transpilers)", MinOccurs = 0, Aliases = "d")]
    public bool describe { get; set; }

    [CommandLineOption(Description = "Describe all of the transpiler in the project", MinOccurs = 0, Aliases = "da")]
    public bool describeAll { get; set; }

    [CommandLineOption(Description = "Input filename or comma separated list of file names", MinOccurs = 0, Aliases = "i")]
    public List<string> input { get; set; }

    [CommandLineOption(Description = "Output filename", MinOccurs = 0, Aliases = "o")]
    public string output { get; set; }

    [CommandLineOption(Description = "Clean all transpilers installed in and downstream of this folder", MinOccurs = 0, Aliases = "c")]
    public bool clean { get; set; }

    [CommandLineOption(Description = "Clean all project transpilers", MinOccurs = 0, Aliases = "ca")]
    public bool cleanAll { get; set; }

    [CommandLineOption(Description = "Cleans only transpilers installed in the current folder", MinOccurs = 0, Aliases = "cl")]
    public bool cleanLocal { get; set; }

    [CommandLineOption(Description = "When supplied with clean, runs clean on all orphaned transpilers", MinOccurs = 0, Aliases = "")]
    public bool purge { get; set; }

    [CommandLineOption(Description = "Don't clean the output before cooking", MinOccurs = 0, Aliases = "sc")]
    public bool skipClean { get; set; }

    [CommandLineOption(Description = "The account which the transpiler belongs to", MinOccurs = 0, Aliases = "a")]
    public string account { get; set; }

    [CommandLineOption(Description = "A list of parameters", MinOccurs = 0, Aliases = "p")]
    public List<string> parameters { get; set; }

    [CommandLineOption(Description = "List of project settings", MinOccurs = 0, Aliases = "ls")]
    public bool listSettings { get; set; }

    [CommandLineOption(Description = "Adds a setting to the SSoT.me Project", MinOccurs = 0, Aliases = "as")]
    public List<string> addSetting { get; set; }

    [CommandLineOption(Description = "Removes a setting from the SSoT.me Project", MinOccurs = 0, Aliases = "rs")]
    public List<string> removeSetting { get; set; }

    [CommandLineOption(Description = "The amount of time to wait for the command to continue", MinOccurs = 0, Aliases = "w")]
    public int waitTimeout { get; set; }

    [CommandLineOption(Description = "Run as this user (look for this user's key file)", MinOccurs = 0, Aliases = "ra")]
    public string runAs { get; set; }

    [CommandLineOption(Description = "Determines if the input should be preserved.", MinOccurs = 0, Aliases = "rz")]
    public bool preserveZFS { get; set; }

    [CommandLineOption(Description = "Executes the given command as a ProcessInfo.Start", MinOccurs = 0, Aliases = "exec")]
    public string execute { get; set; }

    [CommandLineOption(Description = "Include disabled tools in the build", MinOccurs = 0, Aliases = "id")]
    public bool includeDisabled { get; set; }

    [CommandLineOption(Description = "Name of the project (optional parameter to the init command)", MinOccurs = 0, Aliases = "name")]
    public string projectName { get; set; }

    [CommandLineOption(Description = "Name of a group to put a transpiler in within a specific folder", MinOccurs = 0, Aliases = "tg")]
    public string transpilerGroup { get; set; }

    [CommandLineOption(Description = "Add an account api key", MinOccurs = 0, Aliases = "api")]
    public string setAccountAPIKey { get; set; }

    [CommandLineOption(Description = "Launch the SSoT.me website in order to authenticate (and/or register), and then to link that  user to your ssotme CLI.", MinOccurs = 0, Aliases = "auth,login")]
    public bool authenticate { get; set; }

    [CommandLineOption(Description = "Authenticate the ssotme CLI for this specific project (overrides global user login)", MinOccurs = 0, Aliases = "projectAuth")]
    public bool projectLogin { get; set; }

    [CommandLineOption(Description = "Logout of your cli user account", MinOccurs = 0, Aliases = "signout")]
    public bool logout { get; set; }

    [CommandLineOption(Description = "Show configured settings", MinOccurs = 0, Aliases = "")]
    public bool info { get; set; }

    [CommandLineOption(Description = "Show CLI version", MinOccurs = 0, Aliases = "v")]
    public bool version { get; set; }

    [CommandLineOption(Description = "Dry run of a buid", MinOccurs = 0, Aliases = "dr")]
    public bool dryRun { get; set; }

    [CommandLineOption(Description = "TargetUrl of the tool bing invoked", MinOccurs = 0, Aliases = "g")]
    public string targetUrl { get; set; }

    [CommandLineOption(Description = "List all custom tool urls defined for this user", MinOccurs = 0, Aliases = "lu")]
    public bool listUrls { get; set; }

    [CommandLineOption(Description = "View the url for the specified tool", MinOccurs = 0, Aliases = "vu,vt")]
    public string viewUrl { get; set; }

    [CommandLineOption(Description = "Set a tool's URL to a custom endpoint for this user", MinOccurs = 0, Aliases = "su,setToolUrl")]
    public string setUrl { get; set; }

    [CommandLineOption(Description = "Remove a custom tool URL from this user's config, setting it back to the default value.", MinOccurs = 0, Aliases = "ru,removeToolUrl")]
    public string removeUrl { get; set; }

    [CommandLineOption(Description = "Don't let one failing step stop the build: run every remaining transpiler, write the full exception detail of anything that failed to errors.json in the project root, and still exit 0. Defaults to off.", MinOccurs = 0, Aliases = "coe,ignoreErrors,ignoreError")]
    public bool continueOnError { get; set; }

    [CommandLineOption(Description = "List all available versions of the tool", MinOccurs = 0, Aliases = "lv,list,l")]
    public bool listVersions { get; set; }

    [CommandLineOption(Description = "Purge and re-fetch the remote tools index", MinOccurs = 0, Aliases = "rt")]
    public bool refreshTools { get; set; }

    [CommandLineOption(Description = "List all available tools in the current remote tools catalog", MinOccurs = 0, Aliases = "tools,lt")]
    public bool listTools { get; set; }

    [CommandLineOption(Description = "Search the current remote tools catalog by tool name", MinOccurs = 0, Aliases = "search,st")]
    public string searchTools { get; set; }

    [CommandLineOption(Description = "Update the pinned version of this tool to the current head version (does not run the tool)", MinOccurs = 0, Aliases = "up")]
    public bool upgrade { get; set; }

    [CommandLineOption(Description = "Run this tool using the current head version and update its pinned version for this project", MinOccurs = 0, Aliases = "lat")]
    public bool latest { get; set; }

    [CommandLineOption(Description = "View your account's EffortlessAPI subscription plan", MinOccurs = 0, Aliases = "plan")]
    public bool subscription { get; set; }

    [CommandLineOption(Description = "Upgrade the effortless CLI to the latest version", MinOccurs = 0, Aliases = "uc,update")]
    public bool upgradeCli { get; set; }

    [CommandLineOption(Description = "Upgrade all transpilers in the project to the latest version", MinOccurs = 0, Aliases = "ua")]
    public bool upgradeAll { get; set; }
}
