using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Effortless.Cli.E2E.Harness;

/// <summary>
/// Step 14: a stand-in for api.github.com + raw.githubusercontent.com +
/// github.com clone URLs. Repositories are real bare git repositories served
/// over git's dumb HTTP protocol, so <c>git clone</c> works against it.
/// </summary>
internal sealed class MockGitHubServer : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly Dictionary<string, List<Repo>> _accounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _root;
    private readonly Task _listenTask;

    private sealed record Repo(string Account, string Name, string Description, bool IsSeed, string BarePath);

    public MockGitHubServer()
    {
        Port = GetFreePort();
        BaseUri = new Uri($"http://127.0.0.1:{Port}/");
        _root = Path.Combine(Path.GetTempPath(), "effortless-cli-e2e-github", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _listener.Prefixes.Add(BaseUri.ToString());
        _listener.Start();
        _listenTask = ListenAsync();
    }

    public int Port { get; }

    public Uri BaseUri { get; }

    public string ApiBase => new Uri(BaseUri, "api").ToString();

    public string RawBase => new Uri(BaseUri, "raw").ToString();

    public ConcurrentQueue<string> ApiAccountsQueried { get; } = new();

    /// <summary>
    /// The env var UpdateChecker reads to override its GitHub API base
    /// (mirrors EFFORTLESS_SEED_GITHUB_API for SeedCatalogClient). Kept as a
    /// literal here because this black-box harness deliberately has no
    /// project reference to Effortless.Cli.Core.
    /// </summary>
    public const string UpdateCheckApiEnvironmentVariable = "EFFORTLESS_UPDATE_GITHUB_API";

    /// <summary>
    /// The sha served for GET /repos/EffortlessAPI/cli/commits/main (the
    /// UpdateChecker route). Set to a value different from the CLI under
    /// test's CliVersion.CommitSha to simulate a new commit on main.
    /// </summary>
    public string CommitsMainSha { get; set; } = string.Empty;

    public IReadOnlyDictionary<string, string> Environment => new Dictionary<string, string>
    {
        ["EFFORTLESS_SEED_GITHUB_API"] = ApiBase,
        ["EFFORTLESS_SEED_GITHUB_RAW"] = RawBase,
        [UpdateCheckApiEnvironmentVariable] = ApiBase,
    };

    /// <summary>
    /// Adds a repository with the given files committed on <c>main</c>. It is
    /// a seed when <paramref name="files"/> contains effortless.json.
    /// </summary>
    public void AddRepository(
        string account,
        string name,
        string description,
        IReadOnlyDictionary<string, string> files)
    {
        var work = Path.Combine(_root, "work", account, name);
        var bare = Path.Combine(_root, "git", account, name + ".git");
        Directory.CreateDirectory(work);
        foreach (var (relativePath, contents) in files)
        {
            var path = Path.Combine(work, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
        }

        Git(work, "init", "-q", "-b", "main");
        Git(work, "add", "-A");
        Git(work, "-c", "user.name=seed", "-c", "user.email=seed@example.com", "commit", "-q", "-m", "seed");
        Directory.CreateDirectory(Path.GetDirectoryName(bare)!);
        Git(_root, "clone", "--bare", "-q", work, bare);
        Git(bare, "update-server-info");

        if (!_accounts.TryGetValue(account, out var repos))
        {
            repos = [];
            _accounts[account] = repos;
        }

        repos.Add(new Repo(account, name, description, files.ContainsKey("effortless.json"), bare));
    }

    public string CloneUrl(string account, string name) =>
        new Uri(BaseUri, $"git/{account}/{name}.git").ToString();

    public async ValueTask DisposeAsync()
    {
        _stopping.Cancel();
        _listener.Stop();
        _listener.Close();
        try
        {
            await _listenTask;
        }
        catch (Exception)
        {
            // Shutdown races are expected.
        }

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private async Task ListenAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (_stopping.IsCancellationRequested)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var segments = (context.Request.Url?.AbsolutePath ?? "/")
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.UnescapeDataString)
                .ToArray();

            // /api/users/{account}/repos
            if (segments is ["api", "users", var account, "repos"])
            {
                ApiAccountsQueried.Enqueue(account);
                var repos = _accounts.TryGetValue(account, out var list) ? list : [];
                var page = int.Parse(System.Web.HttpUtility.ParseQueryString(context.Request.Url!.Query)["page"] ?? "1");
                var body = page == 1
                    ? repos.Select(repo => new
                    {
                        name = repo.Name,
                        description = repo.Description,
                        clone_url = CloneUrl(repo.Account, repo.Name),
                        default_branch = "main",
                    }).ToArray()
                    : [];
                await WriteAsync(context.Response, 200, "application/json", JsonSerializer.SerializeToUtf8Bytes(body));
                return;
            }

            // /api/repos/EffortlessAPI/cli/commits/main (UpdateChecker)
            if (segments is ["api", "repos", "EffortlessAPI", "cli", "commits", "main"])
            {
                await WriteAsync(
                    context.Response,
                    200,
                    "application/json",
                    JsonSerializer.SerializeToUtf8Bytes(new { sha = CommitsMainSha }));
                return;
            }

            // /raw/{account}/{repo}/{branch}/effortless.json
            if (segments is ["raw", var rawAccount, var rawRepo, "main", "effortless.json"])
            {
                var repo = Find(rawAccount, rawRepo);
                if (repo is { IsSeed: true })
                {
                    await WriteAsync(context.Response, 200, "application/json", Encoding.UTF8.GetBytes("{}"));
                }
                else
                {
                    await WriteAsync(context.Response, 404, "text/plain", Encoding.UTF8.GetBytes("404: Not Found"));
                }

                return;
            }

            // /git/{account}/{repo}.git/<path> : dumb HTTP protocol, static files.
            if (segments.Length >= 4 && segments[0] == "git" && segments[2].EndsWith(".git", StringComparison.Ordinal))
            {
                var repo = Find(segments[1], segments[2][..^4]);
                if (repo is not null)
                {
                    var relative = Path.Combine(segments.Skip(3).ToArray());
                    var full = Path.GetFullPath(Path.Combine(repo.BarePath, relative));
                    if (full.StartsWith(repo.BarePath, StringComparison.Ordinal) && File.Exists(full))
                    {
                        await WriteAsync(context.Response, 200, "application/octet-stream", await File.ReadAllBytesAsync(full));
                        return;
                    }
                }
            }

            await WriteAsync(context.Response, 404, "text/plain", Encoding.UTF8.GetBytes("not found"));
        }
        catch (Exception exception)
        {
            try
            {
                await WriteAsync(context.Response, 500, "text/plain", Encoding.UTF8.GetBytes(exception.ToString()));
            }
            catch
            {
            }
        }
    }

    private Repo? Find(string account, string name) =>
        _accounts.TryGetValue(account, out var repos)
            ? repos.FirstOrDefault(repo => string.Equals(repo.Name, name, StringComparison.OrdinalIgnoreCase))
            : null;

    private static async Task WriteAsync(HttpListenerResponse response, int status, string contentType, byte[] body)
    {
        response.StatusCode = status;
        response.ContentType = contentType;
        response.ContentLength64 = body.Length;
        try
        {
            await response.OutputStream.WriteAsync(body);
        }
        finally
        {
            response.Close();
        }
    }

    private static void Git(string cwd, params string[] args)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)!;
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {stderr}");
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
