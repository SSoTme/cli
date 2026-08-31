using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli.Seeds;

public sealed record SeedRepository(
    string Account,
    string Name,
    string Description,
    string CloneUrl,
    string DefaultBranch)
{
    public string ShortName =>
        Name.Replace("root-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("seed-", string.Empty, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Finds public GitHub repositories that declare themselves as Effortless seeds
/// by containing an effortless.json file at the repository root.
/// </summary>
public sealed class SeedCatalogClient
{
    public const string DefaultAccount = "ssotme";

    private readonly HttpClient _httpClient;

    public SeedCatalogClient(HttpClient httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("effortless-cli", CliVersion.Value));
        }
    }

    public async Task<IReadOnlyList<SeedRepository>> ListAsync(
        string account,
        CancellationToken cancellationToken = default)
    {
        account = string.IsNullOrWhiteSpace(account)
            ? DefaultAccount
            : account.Trim();
        var repositories = new List<SeedRepository>();
        for (var page = 1; ; page++)
        {
            using var response = await _httpClient.GetAsync(
                $"https://api.github.com/users/{Uri.EscapeDataString(account)}/repos?per_page=100&page={page}",
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var root = JArray.Parse(
                await response.Content.ReadAsStringAsync(
                    cancellationToken));
            foreach (var item in root.OfType<JObject>())
            {
                var name = item["name"]?.Value<string>();
                var cloneUrl = item["clone_url"]?.Value<string>();
                var defaultBranch =
                    item["default_branch"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(name)
                    || string.IsNullOrWhiteSpace(cloneUrl)
                    || string.IsNullOrWhiteSpace(defaultBranch))
                {
                    throw new InvalidDataException(
                        "GitHub returned a repository without name, clone_url, or default_branch.");
                }

                if (await HasRootProjectAsync(
                        account,
                        name,
                        defaultBranch,
                        cancellationToken))
                {
                    repositories.Add(
                        new SeedRepository(
                            account,
                            name,
                            item["description"]?.Value<string>()
                            ?? string.Empty,
                            cloneUrl,
                            defaultBranch));
                }
            }

            if (root.Count < 100)
            {
                break;
            }
        }

        return repositories
            .OrderBy(seed => seed.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(seed => seed.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<bool> HasRootProjectAsync(
        string account,
        string repository,
        string defaultBranch,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://raw.githubusercontent.com/{Uri.EscapeDataString(account)}/{Uri.EscapeDataString(repository)}/{Uri.EscapeDataString(defaultBranch)}/effortless.json");
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }
}
