using Effortless.Cli.Config;
using Effortless.Cli.Options;
using Effortless.Cli.Project;

namespace Effortless.Cli.Commands;

public sealed class InfoCommand
{
    private readonly RemoteToolsIndex _index;

    public InfoCommand(RemoteToolsIndex index = null)
    {
        _index = index;
    }

    public int Run(CliInvocation invocation)
    {
        try
        {
            var key = KeyFile.GetKey(
                invocation.Options.runAs);
            var keyLabel = string.IsNullOrEmpty(
                invocation.Options.runAs)
                ? "effortless.key"
                : invocation.Options.runAs + ".key";
            var project = ProjectLocator.TryToLoad(
                new DirectoryInfo(invocation.CurrentDirectory));

            Console.WriteLine(
                $"\nEffortless CLI Version {CliVersion.Value}\n\nConfiguration for `{keyLabel}`:");
            Console.WriteLine(
                $"Project: {project?.Name ?? "<null>"}");

            string loginLabel = null;
            var env = EnvFile.TryLoadFromNearestProject();
            var projectJwt = env?.GetValue("EFFORTLESS_JWT");
            if (!string.IsNullOrEmpty(projectJwt))
            {
                var email = JwtStore.GetEmailFromJwt(projectJwt);
                loginLabel = string.IsNullOrEmpty(email)
                    ? "(unknown) [project]"
                    : $"{email} [project]";
            }
            else
            {
                var store = new JwtStore();
                var globalJwt = store.GetStoredJWTToken();
                if (!string.IsNullOrEmpty(globalJwt))
                {
                    var email = store.GetStoredEmail()
                                ?? JwtStore.GetEmailFromJwt(globalJwt);
                    loginLabel = string.IsNullOrEmpty(email)
                        ? "(unknown) [global]"
                        : $"{email} [global]";
                }
            }

            Console.WriteLine(
                $"Logged in as: {loginLabel ?? "(not logged in)"}");
            Console.WriteLine("Subscription: n/a");
            Console.WriteLine(DescribeCatalog());

            if (key.APIKeys.Count == 0)
            {
                Console.WriteLine("No API keys configured.");
            }
            else
            {
                Console.WriteLine("Configured API keys:");
                foreach (var pair in key.APIKeys)
                {
                    Console.WriteLine($"  {pair.Key}: {pair.Value}");
                }
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"Error retrieving configuration: {exception.Message}");
        }

        return 0;
    }

    /// <summary>
    /// D23 / step 11: catalog age line. info stays offline; this reads only the
    /// cached fetchedAt stamp.
    /// </summary>
    private string DescribeCatalog()
    {
        var fetchedAt = _index?.FetchedAt;
        if (fetchedAt is null)
        {
            return "Catalog: not fetched yet";
        }

        var age = CatalogFreshnessPolicy.FormatAge(_index.CatalogAge ?? TimeSpan.Zero);
        var due = _index.NextRefreshDueAt?.UtcDateTime.ToString(
            "yyyy-MM-ddTHH:mm:ssZ",
            System.Globalization.CultureInfo.InvariantCulture);
        return $"Catalog: fetched {fetchedAt.Value.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}, age {age}, next automatic refresh {due}";
    }
}
