using Effortless.Cli.Options;
using Effortless.Cli.Seeds;

namespace Effortless.Cli.Commands;

public sealed class SeedCommands
{
    private readonly SeedCatalogClient _catalog;
    private readonly SeedRepositoryManager _repositories;

    public SeedCommands(
        SeedCatalogClient catalog = null,
        SeedRepositoryManager repositories = null)
    {
        _catalog = catalog ?? new SeedCatalogClient();
        _repositories =
            repositories ?? new SeedRepositoryManager();
    }

    public int List(CliInvocation invocation)
    {
        var account = invocation.RemainingArguments.FirstOrDefault()
                      ?? Environment.GetEnvironmentVariable(
                          "EFFORTLESS_SEED_GITHUB_ACCOUNT")
                      ?? SeedCatalogClient.DefaultAccount;
        var seeds = _catalog.ListAsync(account)
            .GetAwaiter()
            .GetResult();
        Console.WriteLine(
            $"Effortless seeds from GitHub account '{account}' ({seeds.Count}):");
        foreach (var seed in seeds)
        {
            Console.WriteLine(
                $"  {seed.Name}  {seed.Description}".TrimEnd());
        }

        return 0;
    }

    public int Clone(CliInvocation invocation)
    {
        var requested = invocation.RemainingArguments.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requested))
        {
            throw new ArgumentException(
                "Specify a seed repository name or HTTP(S) clone URL.");
        }

        var destination =
            invocation.RemainingArguments.Skip(1).FirstOrDefault();
        string cloneUrl;
        string defaultDirectory;
        if (Uri.TryCreate(
                requested,
                UriKind.Absolute,
                out var uri)
            && uri.Scheme is "https" or "http")
        {
            cloneUrl = uri.ToString();
            defaultDirectory = Path.GetFileNameWithoutExtension(
                uri.AbsolutePath.TrimEnd('/'));
        }
        else
        {
            var account = Environment.GetEnvironmentVariable(
                              "EFFORTLESS_SEED_GITHUB_ACCOUNT")
                          ?? SeedCatalogClient.DefaultAccount;
            var name = requested;
            var slash = requested.IndexOf('/');
            if (slash > 0)
            {
                account = requested[..slash];
                name = requested[(slash + 1)..];
            }

            var matches = _catalog.ListAsync(account)
                .GetAwaiter()
                .GetResult()
                .Where(seed =>
                    string.Equals(
                        seed.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        seed.ShortName,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    matches.Length == 0
                        ? $"Seed '{requested}' was not found."
                        : $"Seed '{requested}' is ambiguous: {string.Join(", ", matches.Select(seed => seed.Name))}");
            }

            cloneUrl = matches[0].CloneUrl;
            defaultDirectory = matches[0].ShortName;
        }

        destination = string.IsNullOrWhiteSpace(destination)
            ? defaultDirectory
            : destination;
        var clonedPath = _repositories.Clone(
            cloneUrl,
            destination);
        Console.WriteLine(
            $"Cloned Effortless seed to {clonedPath}");
        Console.WriteLine(
            $"Run `cd \"{clonedPath}\" && effortless build` when you are ready to execute its pipeline.");
        return 0;
    }
}
