using System.Net.Http;
using System.Text;
using Effortless.Cli.Config;
using Effortless.Cli.Project;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli.Auth;

/// <summary>
/// The CLI side of the authentication seam (step 13, D18). login / projectLogin /
/// plan / logout call the published <c>effortless/effortless/effortless-auth</c>
/// tool, resolved through the normal catalog path. The service does not enforce
/// accounts yet, so every command tells the truth with the preview line. Tool
/// execution and buildOnTrigger never call this.
/// </summary>
public sealed class MagicLinkAuth
{
    public const string AuthToolName = "effortless/effortless/effortless-auth";
    public const string AuthToolShortName = "effortless-auth";

    public const string PreviewSuffix =
        "(preview: the authentication service does not enforce accounts yet)";

    public const string SignedInMessage = "Signed in " + PreviewSuffix + ".";
    public const string ProjectSignedInMessage = "Project signed in " + PreviewSuffix + ".";

    private readonly Func<string> _resolveAuthToolUrl;
    private readonly Func<string> _resolveAuthToolUrlOffline;
    private readonly HttpClient _httpClient;
    private readonly JwtStore _jwtStore;
    private readonly TextReader _stdin;
    private readonly TextWriter _stdout;

    /// <param name="resolveAuthToolUrl">Catalog resolution with R0 freshness (may refresh).</param>
    /// <param name="resolveAuthToolUrlOffline">Cache-only resolution for best-effort calls such as logout; never refreshes.</param>
    public MagicLinkAuth(
        Func<string> resolveAuthToolUrl,
        Func<string> resolveAuthToolUrlOffline = null,
        HttpClient httpClient = null,
        JwtStore jwtStore = null,
        TextReader stdin = null,
        TextWriter stdout = null)
    {
        _resolveAuthToolUrl = resolveAuthToolUrl
            ?? throw new ArgumentNullException(nameof(resolveAuthToolUrl));
        _resolveAuthToolUrlOffline = resolveAuthToolUrlOffline ?? resolveAuthToolUrl;
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60),
        };
        _jwtStore = jwtStore ?? new JwtStore();
        _stdin = stdin ?? Console.In;
        _stdout = stdout ?? Console.Out;
    }

    /// <summary>POST /login with the prompted email; stores the returned token.</summary>
    public int Login()
    {
        var existingEmail = _jwtStore.IsAuthenticated()
            ? _jwtStore.GetStoredEmail()
            : null;
        if (!string.IsNullOrEmpty(existingEmail))
        {
            _stdout.WriteLine($"You are already authenticated as {existingEmail}.");
            _stdout.Write("Do you want to re-authenticate? (y/N): ");
            var answer = _stdin.ReadLine()?.Trim().ToLowerInvariant();
            if (answer is not ("y" or "yes"))
            {
                _stdout.WriteLine("Authentication cancelled.");
                return 0;
            }
        }

        _stdout.Write("Email: ");
        var email = _stdin.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            Console.Error.WriteLine("An email address is required to sign in.");
            return -1;
        }

        if (!TryCall(HttpMethod.Post, "login", new JObject { ["email"] = email }, out var response))
        {
            return -1;
        }

        var token = response?["token"]?.Value<string>();
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine(
                $"The authentication service accepted the sign-in but returned no token: {response}");
            return -1;
        }

        if (!_jwtStore.StoreJWTToken(token, email))
        {
            return -1;
        }

        _stdout.WriteLine($"{SignedInMessage} Signed in as {email}.");
        return 0;
    }

    /// <summary>POST /project-login for the current project; writes EFFORTLESS_JWT to effortless.env.</summary>
    public int ProjectLogin(EffortlessProject project)
    {
        if (project is null)
        {
            Console.Error.WriteLine(
                "projectLogin needs an effortless.json project in the current directory or one of its parents.");
            return -1;
        }

        var body = new JObject
        {
            ["projectId"] = project.SSoTmeProjectId,
            ["projectName"] = project.Name,
        };
        if (!TryCall(HttpMethod.Post, "project-login", body, out var response))
        {
            return -1;
        }

        var token = response?["token"]?.Value<string>();
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine(
                $"The authentication service accepted the project sign-in but returned no token: {response}");
            return -1;
        }

        var envPath = Path.Combine(project.RootPath, "effortless.env");
        EnvFile.WriteEnvValue(envPath, "EFFORTLESS_JWT", token);
        _stdout.WriteLine($"{ProjectSignedInMessage} Wrote EFFORTLESS_JWT to {envPath}.");
        return 0;
    }

    /// <summary>GET /plan; prints the plan and whether it is enforced.</summary>
    public int Subscription(string jwt)
    {
        if (!TryCall(HttpMethod.Get, "plan", null, out var response, jwt))
        {
            return -1;
        }

        var plan = response?["plan"]?.Value<string>() ?? "unknown";
        var enforced = response?["enforced"]?.Value<bool?>() ?? false;
        var email = string.IsNullOrEmpty(jwt) ? null : _jwtStore.GetStoredEmail() ?? JwtStore.GetEmailFromJwt(jwt);
        _stdout.WriteLine(
            enforced
                ? $"Plan: {plan} (enforced)."
                : $"Plan: {plan} (not enforced yet: the authentication service does not enforce accounts).");
        _stdout.WriteLine(
            string.IsNullOrEmpty(email)
                ? "Not signed in; the service does not require it yet."
                : $"Signed in as {email}.");
        return 0;
    }

    public string GetPlan(string jwt)
    {
        return TryCall(HttpMethod.Get, "plan", null, out var response, jwt, quiet: true)
            ? response?["plan"]?.Value<string>()
            : null;
    }

    /// <summary>POST /logout, best effort: the local tokens are already cleared.</summary>
    public void LogoutBestEffort(string jwt)
    {
        TryCall(HttpMethod.Post, "logout", new JObject(), out _, jwt, quiet: true);
    }

    private bool TryCall(
        HttpMethod method,
        string route,
        JObject body,
        out JObject response,
        string jwt = null,
        bool quiet = false)
    {
        response = null;
        string baseUrl;
        try
        {
            baseUrl = quiet ? _resolveAuthToolUrlOffline() : _resolveAuthToolUrl();
        }
        catch (Exception exception)
        {
            if (!quiet)
            {
                Console.Error.WriteLine(exception.Message);
            }

            return false;
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            if (!quiet)
            {
                Console.Error.WriteLine(
                    $"The authentication tool '{AuthToolName}' is not in the remote tools catalog. Run 'effortless -refreshTools' and try again, or point at a local build with 'effortless -setToolUrl {AuthToolShortName}=http://localhost:30080'.");
            }

            return false;
        }

        var url = baseUrl.TrimEnd('/') + "/" + route;
        try
        {
            using var request = new HttpRequestMessage(method, url);
            if (body is not null)
            {
                request.Content = new StringContent(
                    body.ToString(Newtonsoft.Json.Formatting.None),
                    Encoding.UTF8,
                    "application/json");
            }

            if (!string.IsNullOrEmpty(jwt))
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + jwt);
            }

            using var httpResponse = _httpClient.Send(request);
            var content = httpResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!httpResponse.IsSuccessStatusCode)
            {
                if (!quiet)
                {
                    Console.Error.WriteLine(
                        $"The authentication service returned {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase} for {method} {url}: {Trim(content)}");
                }

                return false;
            }

            try
            {
                response = string.IsNullOrWhiteSpace(content) ? new JObject() : JObject.Parse(content);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                if (!quiet)
                {
                    Console.Error.WriteLine(
                        $"The authentication service returned a non-JSON response for {method} {url}: {Trim(content)}");
                }

                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            if (!quiet)
            {
                Console.Error.WriteLine(
                    $"Could not reach the authentication service at {url}: {exception.Message}");
            }

            return false;
        }
    }

    private static string Trim(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return "(empty body)";
        }

        var single = content.Replace("\r", " ").Replace("\n", " ");
        return single.Length <= 200 ? single : single[..200] + "...";
    }
}
