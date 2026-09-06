using Effortless.Cli.Options;
using Effortless.Cli.Project;

namespace Effortless.Cli.Config;

public sealed class CredentialResolver
{
    public void AddProjectSettings(CliInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        invocation.Options.parameters ??= new List<string>();

        foreach (var setting in invocation.Project?.ProjectSettings
                     ?? Enumerable.Empty<ProjectSetting>())
        {
            if (string.IsNullOrEmpty(setting?.Name))
            {
                continue;
            }

            var prefix = setting.Name + "=";
            if (!invocation.Options.parameters.Any(
                    value => value.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase)))
            {
                invocation.Options.parameters.Add(
                    $"{setting.Name}={setting.Value}");
            }
        }
    }

    public void Resolve(CliInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        if (string.IsNullOrEmpty(invocation.Account))
        {
            return;
        }

        invocation.Options.parameters ??= new List<string>();
        var env = EnvFile.TryLoadFromNearestProject(
            invocation.Options.debug);
        var envParameters = env?.ResolveAccountParams(invocation.Account)
                            ?? new Dictionary<string, string>();

        foreach (var parameter in envParameters)
        {
            ReplaceParameter(
                invocation.Options.parameters,
                parameter.Key,
                parameter.Value);
        }

        if (envParameters.ContainsKey("apiKey"))
        {
            return;
        }

        var key = KeyFile.GetKey(invocation.Options.runAs);
        if (!key.APIKeys.TryGetValue(invocation.Account, out var apiKey))
        {
            return;
        }

        if (apiKey.TrimStart().StartsWith("{", StringComparison.Ordinal)
            || apiKey.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            throw new Exception(
                $"The API key for account '{invocation.Account}' is in JSON format and cannot be used directly as apiKey parameter. Please store simple string API keys in the format: effortless -setAccountAPIKey={invocation.Account}/YOUR_API_KEY");
        }

        ReplaceParameter(invocation.Options.parameters, "apiKey", apiKey);
    }

    private static void ReplaceParameter(
        List<string> parameters,
        string name,
        string value)
    {
        var prefix = name + "=";
        parameters.RemoveAll(
            parameter => parameter.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase));
        parameters.Add($"{name}={value}");
    }
}
