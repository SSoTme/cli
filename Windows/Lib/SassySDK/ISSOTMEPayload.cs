using System;
using System.Collections.Generic;
using SSoTme.OST.Lib.DataClasses;
using SSoTme.OST.Lib.SassySDK.Derived;

namespace SassyMQ.SSOTME.Lib.RMQActors
{
    public interface ISSOTMEPayload
    {
        Guid AccessToken { get; set; }
        string AuthToken { get; set; }
        string BaseRoutingKey { get; set; }
        string CLIAccount { get; set; }
        List<string> CLIInput { get; set; }
        string CLIInputFileContents { get; set; }
        string CLIInputFileSetJson { get; set; }
        string CLIInputFileSetXml { get; set; }
        string CLIOutput { get; set; }
        List<string> CLIParams { get; set; }
        string CLITranspiler { get; set; }
        int CLIWaitTimeout { get; set; }
        string EmailAddress { get; set; }
        List<FileType> FileTypes { get; set; }
        string InstanceName { get; set; }
        bool IsDirectMessage { get; set; }
        bool IsNewAccount { get; set; }
        List<PublicAccount> PublicAccounts { get; set; }
        string RecoverEmail { get; set; }
        string RegisterEmailAddress { get; set; }
        bool RegisterEmailSent { get; set; }
        string RegisterName { get; set; }
        bool ReturnJson { get; set; }
        string ScreenName { get; set; }
        string Secret { get; set; }
        Dictionary<string, string> Settings { get; set; }
        SSOTMEKey SSoTmeKey { get; set; }
        SSoTmeProject SSoTmeProject { get; set; }
        Transpiler Transpiler { get; set; }
        TranspileRequest TranspileRequest { get; set; }
        TranspilerHost TranspilerHost { get; set; }
        TranspilerInstance TranspilerInstance { get; set; }
        string TranspilerName { get; set; }
        List<Transpiler> Transpilers { get; set; }

        void CleanFileSet(bool deleteEmptyDirs = true);
        string GetParameterByIndex(int parameterIndex);
        string GetParameterByName(string paramName);
        string GetSetting(string settingName, string defaultStringValue);
        bool HasParamNamed(string paramName);
        int SaveFileSet(bool skipClean);
        void SavePreviousFileSet(string fileSetXml);
        string ToString();
    }
}