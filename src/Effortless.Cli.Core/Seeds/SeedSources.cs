#nullable enable
using System.Text.RegularExpressions;
using Effortless.Cli.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli.Seeds;

/// <summary>
/// One entry of the ordered seed-source list: a GitHub user or organization.
/// </summary>
public sealed record SeedSource(string Account, bool IsDefault, bool IsFromEnvironment)
{
    public string Marker => IsFromEnvironment ? " (env)" : IsDefault ? " (default)" : string.Empty;
}

/// <summary>
/// The ordered list of GitHub accounts searched for Effortless seeds (step 14):
/// <c>~/.effortless/seed_sources.json</c>, or the two defaults when the file
/// is absent. <c>EFFORTLESS_SEED_GITHUB_ACCOUNT</c> is prepended for one
/// invocation without being written.
/// </summary>
public sealed class SeedSources
{
    public const string FileName = "seed_sources.json";
    public const string EnvironmentVariable = "EFFORTLESS_SEED_GITHUB_ACCOUNT";
    public static readonly IReadOnlyList<string> Defaults = ["ssotme", "effortlessapi"];

    private static readonly Regex ValidAccount =
        new("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$", RegexOptions.Compiled);

    private readonly string _path;
    private readonly string? _environmentAccount;

    public SeedSources(string? path = null, string? environmentAccount = null)
    {
        _path = path ?? Path.Combine(UserConfigDir.EffortlessDir.FullName, FileName);
        _environmentAccount = environmentAccount
            ?? Environment.GetEnvironmentVariable(EnvironmentVariable);
    }

    public string FilePath => _path;

    public static bool IsValidAccount(string? account) =>
        !string.IsNullOrWhiteSpace(account) && ValidAccount.IsMatch(account);

    /// <summary>
    /// The effective search order: the environment account (if any) first,
    /// then the stored list or the defaults.
    /// </summary>
    public IReadOnlyList<SeedSource> Load()
    {
        var stored = ReadFile();
        var result = new List<SeedSource>();
        if (!string.IsNullOrWhiteSpace(_environmentAccount))
        {
            result.Add(new SeedSource(_environmentAccount.Trim(), IsDefault: false, IsFromEnvironment: true));
        }

        foreach (var account in stored ?? Defaults)
        {
            if (!result.Any(existing => Same(existing.Account, account)))
            {
                result.Add(new SeedSource(account, IsDefault: stored is null, IsFromEnvironment: false));
            }
        }

        return result;
    }

    /// <summary>The stored (or default) accounts only, without the env prepend.</summary>
    public IReadOnlyList<string> LoadStored() => ReadFile() ?? Defaults.ToList();

    /// <summary>Returns false when the account was already listed.</summary>
    public bool Add(string account)
    {
        account = Validate(account);
        var stored = LoadStored().ToList();
        if (stored.Any(existing => Same(existing, account)))
        {
            return false;
        }

        stored.Add(account);
        Write(stored);
        return true;
    }

    /// <summary>Returns false when the account was not listed.</summary>
    public bool Remove(string account)
    {
        account = Validate(account);
        var stored = LoadStored().ToList();
        var removed = stored.RemoveAll(existing => Same(existing, account));
        if (removed == 0)
        {
            return false;
        }

        Write(stored);
        return true;
    }

    private static string Validate(string account)
    {
        if (!IsValidAccount(account))
        {
            throw new ArgumentException(
                $"ERROR: '{account}' is not a valid GitHub account name.");
        }

        return account.Trim();
    }

    private List<string>? ReadFile()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        JObject document;
        try
        {
            document = JObject.Parse(File.ReadAllText(_path));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"{_path} is not valid JSON: {exception.Message}",
                exception);
        }

        var sources = document["sources"] as JArray
            ?? throw new InvalidDataException(
                $"{_path} must contain a \"sources\" array of GitHub account names.");
        var accounts = new List<string>();
        foreach (var token in sources)
        {
            var account = token.Value<string>();
            if (!IsValidAccount(account))
            {
                throw new InvalidDataException(
                    $"{_path} lists '{account}', which is not a valid GitHub account name.");
            }

            if (!accounts.Any(existing => Same(existing, account!)))
            {
                accounts.Add(account!.Trim());
            }
        }

        return accounts;
    }

    private void Write(IReadOnlyList<string> accounts)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(
            _path,
            new JObject { ["sources"] = new JArray(accounts) }
                .ToString(Formatting.Indented) + Environment.NewLine);
    }

    private static bool Same(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
