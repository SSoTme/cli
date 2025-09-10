using Plossum.CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSoTme.OST.Lib.CLIOptions
{

    [CommandLineManager(ApplicationName = "SSoTme CLI",
                        Copyright = "Copyright 2025, EffortlessAPI.com",
                        Description = @"-p description=\n\nSYNTAX: ssotme {command} [...{additional_args}] [options]\nOptions")]
    public partial class SSoTmeCLIHandler
    {
        
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
        
        [CommandLineOption(Description = "Builds only the root level transpilers, not the sub-directories.", MinOccurs = 0, Aliases = "bl,replaylocal,rebuildLocal,pullLocal")]
        public bool buildLocal { get; set; }
        
        [CommandLineOption(Description = "Builds whenever a trigger is invoked (see readme for URL)", MinOccurs = 0, Aliases = "bot")]
        public string buildOnTrigger { get; set; }
        
        [CommandLineOption(Description = "Use with buildOnTrigger to connect the baseId to the SSoTme Copilot Agent", MinOccurs = 0, Aliases = "cc")]
        public bool copilotConnect { get; set; }
        
        [CommandLineOption(Description = "Describes the current SSoT.me Project (and all transpilers)", MinOccurs = 0, Aliases = "d")]
        public bool describe { get; set; }
        
        [CommandLineOption(Description = "Descibe all of the transpiler in the project", MinOccurs = 0, Aliases = "da")]
        public bool descibeAll { get; set; }
        
        [CommandLineOption(Description = "Discuss the project with an AI", MinOccurs = 0, Aliases = "ai")]
        public bool discuss { get; set; }
        
        [CommandLineOption(Description = "Input filename or comma separated list of file names", MinOccurs = 0, Aliases = "i")]
        public List<string> input { get; set; }
        
        [CommandLineOption(Description = "Output filename", MinOccurs = 0, Aliases = "o")]
        public string output { get; set; }
        
        [CommandLineOption(Description = "Don't output the final results - instead, clean", MinOccurs = 0, Aliases = "c")]
        public bool clean { get; set; }
        
        [CommandLineOption(Description = "Don't output the final results - instead, clean", MinOccurs = 0, Aliases = "ca")]
        public bool cleanAll { get; set; }
        
        [CommandLineOption(Description = "Don't clean the output before cooking", MinOccurs = 0, Aliases = "sc")]
        public bool skipClean { get; set; }
        
        [CommandLineOption(Description = "A local guide that will connect to the my.effortlessapi.com Guide", MinOccurs = 0, Aliases = "lg")]
        public bool localGuide { get; set; }
        
        [CommandLineOption(Description = "Lists seeds available to be clones", MinOccurs = 0, Aliases = "lsd")]
        public bool listSeeds { get; set; }
        
        [CommandLineOption(Description = "Clones a specified seed", MinOccurs = 0, Aliases = "cs,clone")]
        public bool cloneSeed { get; set; }
        
        [CommandLineOption(Description = "The account which the transpiler belongs to", MinOccurs = 0, Aliases = "a")]
        public string account { get; set; }
        
        [CommandLineOption(Description = "A list of parameters", MinOccurs = 0, Aliases = "p")]
        public List<string> parameters { get; set; }
        
        [CommandLineOption(Description = "List of project settings", MinOccurs = 0, Aliases = "ls")]
        public bool listSettings { get; set; }
        
        [CommandLineOption(Description = "Adds a setting to the SSoT.me Project", MinOccurs = 0, Aliases = "as")]
        public List<string> addSetting { get; set; }
        
        [CommandLineOption(Description = "REmoves a setting from the SSoT.me Project", MinOccurs = 0, Aliases = "rs")]
        public List<string> removeSetting { get; set; }
        
        [CommandLineOption(Description = "The keyfile to use.  By default it looks for ~/.ssotme/ssotme.key. (or ~/.ssotme/ssotme.{username}.key)", MinOccurs = 0, Aliases = "f")]
        public string keyFile { get; set; }
        
        [CommandLineOption(Description = "Add a transpiler to for the given account", MinOccurs = 0, Aliases = "")]
        public string addTranspiler { get; set; }
        
        [CommandLineOption(Description = "Delete the transpiler with the given name", MinOccurs = 0, Aliases = "")]
        public string deleteTranspiler { get; set; }
        
        [CommandLineOption(Description = "The amount of time to wait for the command to continue", MinOccurs = 0, Aliases = "w")]
        public int waitTimeout { get; set; }
        
        [CommandLineOption(Description = "Run as this user (look for this user's key file)", MinOccurs = 0, Aliases = "ra")]
        public string runAs { get; set; }
        
        [CommandLineOption(Description = "Determines if the input should be preserved.", MinOccurs = 0, Aliases = "rz")]
        public bool preserveZFS { get; set; }
        
        [CommandLineOption(Description = "Checks the result of a build linking up input and output files of the transpiles.  Creates a SPXML file in the DSPXml folder of the project.", MinOccurs = 0, Aliases = "cr")]
        public bool checkResults { get; set; }
        
        [CommandLineOption(Description = "Creates documentation based on a DSPXml file created with the -checkResults flag.", MinOccurs = 0, Aliases = "cd")]
        public bool createDocs { get; set; }
        
        [CommandLineOption(Description = "Executes the given command as a ProcessInfo.Start", MinOccurs = 0, Aliases = "exec")]
        public string execute { get; set; }
        
        [CommandLineOption(Description = "Include disabled tools in th ebuild", MinOccurs = 0, Aliases = "id")]
        public bool includeDisabled { get; set; }
        
        [CommandLineOption(Description = "Name of the project (optional parameter to the init command)", MinOccurs = 0, Aliases = "name")]
        public string projectName { get; set; }
        
        [CommandLineOption(Description = "Name of a group to put a transpiler in within a specific folder", MinOccurs = 0, Aliases = "tg")]
        public string transpilerGroup { get; set; }
        
        [CommandLineOption(Description = "Add an account api key", MinOccurs = 0, Aliases = "api")]
        public string setAccountAPIKey { get; set; }
        
        [CommandLineOption(Description = "Launch the SSoT.me website in order to authenticate (and/or register), and then to link that  user to your ssotme CLI.", MinOccurs = 0, Aliases = "auth")]
        public bool authenticate { get; set; }
        
        [CommandLineOption(Description = "Override the default URL specified by the seed repository", MinOccurs = 0, Aliases = "")]
        public string repoUrl { get; set; }
        
        [CommandLineOption(Description = "Use the beta repository for this seed?", MinOccurs = 0, Aliases = "")]
        public bool betaRepo { get; set; }
        
        [CommandLineOption(Description = "Skips the build part of cloning a Seed repository", MinOccurs = 0, Aliases = "")]
        public bool skipBuild { get; set; }
        
        [CommandLineOption(Description = "Show configured settings", MinOccurs = 0, Aliases = "")]
        public bool info { get; set; }
        
        [CommandLineOption(Description = "Show CLI version", MinOccurs = 0, Aliases = "v")]
        public bool version { get; set; }
        
        [CommandLineOption(Description = "Dry run of a buid", MinOccurs = 0, Aliases = "dr")]
        public bool dryRun { get; set; }
        
        [CommandLineOption(Description = "TargetUrl of the tool bing invoked", MinOccurs = 0, Aliases = "g")]
        public string targetUrl { get; set; }
        
    }
}
