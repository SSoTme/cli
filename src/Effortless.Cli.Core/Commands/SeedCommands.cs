using Effortless.Cli.Options;
using Effortless.Cli.Seeds;

namespace Effortless.Cli.Commands;

public sealed class SeedCommands
{
    private readonly SeedCatalogClient _catalog;
    private readonly SeedRepositoryManager _repositories;
    private readonly Func<SeedSources> _sources;

    public SeedCommands(
        SeedCatalogClient catalog = null,
        SeedRepositoryManager repositories = null,
        Func<SeedSources> sources = null)
    {
        _catalog = catalog ?? new SeedCatalogClient();
        _repositories =
            repositories ?? new SeedRepositoryManager();
        _sources = sources ?? (() => new SeedSources());
    }

    public int ListSources()
    {
        PrintSources(_sources().Load());
        return 0;
    }

    public int AddSource(string account)
    {
        var sources = _sources();
        try
        {
            Console.WriteLine(
                sources.Add(account)
                    ? $"Added seed source '{account}'."
                    : $"Seed source '{account}' is already listed.");
        }
        catch (ArgumentException exception)
        {
            WriteError(exception.Message);
            return -1;
        }

        PrintSources(sources.Load());
        return 0;
    }

    public int RemoveSource(string account)
    {
        var sources = _sources();
        try
        {
            if (!sources.Remove(account))
            {
                WriteError(
                    $"ERROR: Seed source '{account}' is not listed. Current sources: {string.Join(", ", sources.LoadStored())}");
                return -1;
            }
        }
        catch (ArgumentException exception)
        {
            WriteError(exception.Message);
            return -1;
        }

        Console.WriteLine($"Removed seed source '{account}'.");
        PrintSources(sources.Load());
        return 0;
    }

    public int List(CliInvocation invocation)
    {
        var requestedAccount = invocation.RemainingArguments.FirstOrDefault();
        var accounts = string.IsNullOrWhiteSpace(requestedAccount)
            ? _sources().Load().Select(source => source.Account).ToList()
            : [requestedAccount];
        foreach (var account in accounts)
        {
            var seeds = _catalog.ListAsync(account)
                .GetAwaiter()
                .GetResult();
            Console.WriteLine(
                $"Effortless seeds from GitHub account '{account}' ({seeds.Count}):");
            if (seeds.Count == 0)
            {
                Console.WriteLine("  (none)");
            }

            foreach (var seed in seeds)
            {
                Console.WriteLine(
                    $"  {seed.Name}  {seed.Description}".TrimEnd());
            }
        }

        return 0;
    }

    public int Clone(CliInvocation invocation)
    {
        var requested = invocation.RemainingArguments.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requested))
        {
            throw new ArgumentException(
                "Specify a seed as account/repo, a repository name, or an HTTP(S) clone URL.");
        }

        var destination =
            invocation.RemainingArguments.Skip(1).FirstOrDefault();
        string cloneUrl;
        string defaultDirectory;
        string label;
        if (Uri.TryCreate(
                requested,
                UriKind.Absolute,
                out var uri)
            && uri.Scheme is "https" or "http")
        {
            cloneUrl = uri.ToString();
            defaultDirectory = Path.GetFileNameWithoutExtension(
                uri.AbsolutePath.TrimEnd('/'));
            label = cloneUrl;
        }
        else
        {
            var slash = requested.IndexOf('/');
            SeedRepository seed;
            if (slash > 0)
            {
                seed = FindInAccount(
                    requested[..slash],
                    requested[(slash + 1)..],
                    requested);
            }
            else
            {
                seed = FindAcrossSources(requested);
                if (seed is null)
                {
                    return -1;
                }

                Console.WriteLine(
                    $"Found '{requested}' in seed source '{seed.Account}'.");
            }

            cloneUrl = seed.CloneUrl;
            defaultDirectory = seed.ShortName;
            label = $"{seed.Account}/{seed.Name}";
        }

        destination = string.IsNullOrWhiteSpace(destination)
            ? defaultDirectory
            : destination;
        var clonedPath = _repositories.Clone(
            cloneUrl,
            destination);
        Console.WriteLine(
            $"Cloned Effortless seed {label} to {clonedPath}");
        Console.WriteLine(
            $"Run `cd \"{clonedPath}\" && effortless build` when you are ready to execute its pipeline.");
        return 0;
    }

    private SeedRepository FindInAccount(
        string account,
        string name,
        string requested)
    {
        var matches = Matches(account, name);
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                matches.Count == 0
                    ? $"Seed '{requested}' was not found."
                    : $"Seed '{requested}' is ambiguous: {string.Join(", ", matches.Select(seed => $"{seed.Account}/{seed.Name}"))}. Use account/repo.");
        }

        return matches[0];
    }

    private SeedRepository FindAcrossSources(string name)
    {
        var sources = _sources().Load()
            .Select(source => source.Account)
            .ToList();
        var matches = sources
            .SelectMany(account => Matches(account, name))
            .ToList();
        if (matches.Count == 1)
        {
            return matches[0];
        }

        WriteError(
            matches.Count == 0
                ? $"Seed '{name}' was not found in any seed source ({string.Join(", ", sources)})."
                : $"Seed '{name}' is ambiguous: {string.Join(", ", matches.Select(seed => $"{seed.Account}/{seed.Name}"))}. Use account/repo.");
        return null;
    }

    private List<SeedRepository> Matches(string account, string name) =>
        _catalog.ListAsync(account)
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
            .ToList();

    private static void PrintSources(IReadOnlyList<SeedSource> sources)
    {
        Console.WriteLine("Seed sources (searched in order):");
        foreach (var source in sources)
        {
            Console.WriteLine($"  {source.Account}{source.Marker}");
        }
    }

    private static void WriteError(string message)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ForegroundColor = previous;
    }
}
