using Newtonsoft.Json;
using SassyMQ.Lib.RabbitMQ;
using SassyMQ.Lib.RabbitMQ.Payload;
using SSoTme.OST.Lib.CLIOptions;
using SSoTme.OST.Lib.DataClasses;
using SSoTme.OST.Lib.Extensions;
using SSoTme.OST.Lib.SassySDK.Derived;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;


namespace SassyMQ.SSOTME.Lib.RMQActors
{
    public class SSOTMEPayload : StandardPayload<SSOTMEPayload>, ISSOTMEPayload
    {
        private string _cleanFileSetRelativePath;

        static SSOTMEPayload()
        {
            //SSOTMEExtensions.FileWritten += CDVExtensions_FileWritten;
        }

        private string GetSSoTmeDirName()
        {
            var d = this.SSoTmeProject.GetSSoTmeDI();
            return d.ToString();
        }

        private static void CDVExtensions_FileWritten(object sender, EventArgs e)
        {
            FileInfo fi = new FileInfo(sender.SafeToString());
            if (fi.Name == "output.txt" || fi.Name == "search.txt") Console.WriteLine(File.ReadAllText(fi.FullName));
            Console.WriteLine(sender.SafeToString());
        }

        public string GetParameterByIndex(int parameterIndex)
        {
            if (!this.CLIParams.Skip(parameterIndex).Any()) throw new Exception(string.Format("Parameter {0} not found.", parameterIndex));
            else
            {
                var param = this.CLIParams.Skip(parameterIndex).First();
                return param.Substring(param.IndexOf("=") + 1);
            }
        }

        public bool HasParamNamed(string paramName)
        {
            return this.CLIParams.Any(anyParam => anyParam.StartsWith(String.Format("{0}=", paramName)));
        }

        public string GetParameterByName(string paramName)
        {
            if (!this.HasParamNamed(paramName)) throw new Exception(string.Format("Parameter {0} not found.", paramName));
            else
            {
                var param = this.CLIParams.First(firstParam => firstParam.StartsWith(String.Format("{0}=", paramName)));
                return param.Substring(param.IndexOf("=") + 1);
            }
        }


        public SSOTMEPayload()
        {
            this.Settings = new Dictionary<string, string>();
            this.CLIParams = new List<string>();
        }


        public Guid AccessToken { get; set; }
        public string EmailAddress { get; set; }
        public TranspilerHost TranspilerHost { get; set; }
        public string InstanceName { get; set; }
        public bool IsDirectMessage { get; set; }
        public string RecoverEmail { get; set; }
        public string RegisterEmailAddress { get; set; }
        public string RegisterName { get; set; }
        public Dictionary<String, String> Settings { get; set; }

        public string GetSetting(string settingName, string defaultStringValue)
        {
            if (!this.Settings.ContainsKey(settingName) || String.IsNullOrEmpty(this.Settings[settingName]))
            {
                return defaultStringValue;
            }
            else return this.Settings[settingName];
        }

        public string AuthToken { get; set; }
        public string ScreenName { get; set; }
        public string Secret { get; set; }
        public Transpiler Transpiler { get; set; }
        public TranspileRequest TranspileRequest { get; set; }
        public TranspilerInstance TranspilerInstance { get; set; }
        public string TranspilerName { get; set; }
        public List<Transpiler> Transpilers { get; set; }
        public string BaseRoutingKey { get; set; }
        public bool RegisterEmailSent { get; set; }
        public bool IsNewAccount { get; set; }
        public List<PublicAccount> PublicAccounts { get; set; }

        public string CLIAccount { get; set; }
        public List<string> CLIInput { get; set; }
        public string CLIInputFileContents { get; set; }
        public string CLIInputFileSetJson { get; set; }
        public string CLIInputFileSetXml { get; set; }
        public string CLIOutput { get; set; }
        public List<String> CLIParams { get; set; }
        public String CLITranspiler { get; set; }
        public int CLIWaitTimeout { get; set; }
        public List<FileType> FileTypes { get; set; }
        public SSoTmeProject SSoTmeProject { get; set; }
        public SSOTMEKey SSoTmeKey { get; set; }
        public bool ReturnJson { get; set; }

        public void CleanFileSet()
        {
            // skip cleaning if no project
            if (this.SSoTmeProject is null) return;
            
            FileInfo zfsFI = GetZFSFI();
            // Store the relative path from CleanFileSet for use in SavePreviousFileSet
            _cleanFileSetRelativePath = GetCurrentRelativePath();
            if (zfsFI.Exists)
            {
                var previousFileSet = File.ReadAllBytes(zfsFI.FullName);
                previousFileSet.CleanZippedFileSet();
            }

        }

        private string GetCurrentRelativePath()
        {
            string curDir = String.Empty;
            curDir = Environment.CurrentDirectory;

            string relPath;
            if (curDir.StartsWith(this.SSoTmeProject.RootPath, StringComparison.OrdinalIgnoreCase))
            {
                relPath = curDir.Substring(this.SSoTmeProject.RootPath.Length);
                // Normalize path separators and remove leading separators
                relPath = relPath.Replace('\\', '/').TrimStart('/');
            }
            else
            {
                // If current directory is not under project root, use empty relative path
                relPath = "";
            }
            return relPath;
        }

        public override string ToString()
        {
            return String.Format("{0} - {1}", this.SenderId, this.RoutingKey);
        }

        public int SaveFileSet(bool skipClean)
        {
            if (!skipClean) this.CleanFileSet();

            // Save the file set to the disk
            var fileSetXml = this.TranspileRequest.ZippedOutputFileSet.UnzipToString();
            string workingDir = GetSSoTmeDirName();

            var tempFI = new FileInfo(Path.Combine(workingDir, String.Format("tempFileSet_{0}.xml", Guid.NewGuid())));
            File.WriteAllText(tempFI.FullName, fileSetXml);

            // Use the stored relative path to determine where files should be extracted
            string extractToDir = this.SSoTmeProject?.RootPath ?? workingDir;
            if (!string.IsNullOrEmpty(_cleanFileSetRelativePath))
            {
                extractToDir = Path.Combine(this.SSoTmeProject.RootPath, _cleanFileSetRelativePath);
            }
            SSoTme.OST.Lib.Extensions.SSOTMEExtensions.SplitFileSetFile(tempFI.FullName, extractToDir);
            tempFI.Delete();

            this.SavePreviousFileSet(fileSetXml);

            return 0;
        }

        public void SavePreviousFileSet(string fileSetXml)
        {
            // skip for null projects
            if (this.SSoTmeProject is null) return;

            var zfsFI = this.GetZFSFI();
            if (!zfsFI.Directory.Exists) zfsFI.Directory.Create();
            File.WriteAllBytes(zfsFI.FullName, fileSetXml.Zip());

            // DEBUG: Write unzipped XML for debugging
            var xmlDebugPath = Path.ChangeExtension(zfsFI.FullName, ".xml");
            File.WriteAllText(xmlDebugPath, fileSetXml);
            Console.WriteLine($"DEBUG: Wrote XML to {xmlDebugPath}");
        }

        private FileInfo GetZFSFI()
        {
            string curDir = String.Empty;
            string relPath;
            try {
                curDir = Environment.CurrentDirectory;
                if (curDir.StartsWith(this.SSoTmeProject.RootPath, StringComparison.OrdinalIgnoreCase))
                {
                    relPath = curDir.Substring(this.SSoTmeProject.RootPath.Length);
                    // Normalize path separators and remove leading separators
                    relPath = relPath.Replace('\\', '/').TrimStart('/');
                }
                else
                {
                    // If current directory is not under project root, use stored relative path from CleanFileSet
                    relPath = _cleanFileSetRelativePath ?? "";
                }
            } catch (Exception) {
                relPath = _cleanFileSetRelativePath;
            }
            var ssotmeDI = new DirectoryInfo(String.Format("{0}/.ssotme", this.SSoTmeProject.RootPath));
            if (!ssotmeDI.Exists)
            {
                ssotmeDI.Create();
                ssotmeDI.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
            }

            var zfsFileName = String.Format("{0}/{1}/{2}.zfs", ssotmeDI.FullName, relPath, this.Transpiler.LowerHyphenName);
            //var zfsFileName = String.Format("{0}.zfs", this.Transpiler.LowerHyphenName);
            var zfsFI = new FileInfo(zfsFileName);
            return zfsFI;
        }

        internal void SaveCLIOptions(SSoTmeCLIHandler sSoTmeCLIHandler)
        {
            this.CLIAccount = sSoTmeCLIHandler.account;
            this.CLIInput = sSoTmeCLIHandler.input;
            this.CLIInputFileContents = sSoTmeCLIHandler.inputFileContents;
            this.CLIInputFileSetXml = sSoTmeCLIHandler.inputFileSetXml;
            this.CLIOutput = sSoTmeCLIHandler.output;
            this.CLIParams = sSoTmeCLIHandler.parameters;
            this.CLITranspiler = sSoTmeCLIHandler.transpiler;
            this.CLIWaitTimeout = sSoTmeCLIHandler.waitTimeout;
        }
    }
}

