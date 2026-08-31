using Effortless.Cli.Config;
using Effortless.Cli.Project;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli.Auth;

public sealed class MagicLinkAuth
{
    private readonly CloudBridgeClient _bridge;
    private readonly JwtStore _jwtStore;

    public MagicLinkAuth(
        CloudBridgeClient bridge,
        JwtStore jwtStore = null)
    {
        _bridge = bridge;
        _jwtStore = jwtStore ?? new JwtStore();
    }

    public void Login()
    {
        Console.WriteLine("=== EffortlessAPI Authentication ===");
        Console.WriteLine();

        if (_jwtStore.IsAuthenticated()
            && !_jwtStore.IsTokenExpired())
        {
            var email = _jwtStore.GetStoredEmail();
            Console.WriteLine(
                string.IsNullOrEmpty(email)
                    ? "You are already authenticated."
                    : $"You are already authenticated as {email}.");
            Console.Write("Do you want to re-authenticate? (y/N): ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (response is not ("y" or "yes"))
            {
                Console.WriteLine("Authentication cancelled.");
                return;
            }

            Console.WriteLine();
        }

        var result = RunMagicLinkFlow();
        if (result is null)
        {
            return;
        }

        if (!_jwtStore.StoreJWTToken(result.Value.Token, result.Value.Email))
        {
            Console.WriteLine(
                "Authentication successful but failed to store token locally.");
            return;
        }

        Console.WriteLine("Authentication successful!");
    }

    public void ProjectLogin()
    {
        Console.WriteLine("=== Project Authentication ===");
        Console.WriteLine();
        var root = ProjectLocator.FindNearestProjectRoot();
        if (root is null)
        {
            Console.WriteLine(
                "No project found. Run this from a directory with effortless.json, ssotme.json, or aicapture.json.");
            return;
        }

        var envPath = Path.Combine(root, "effortless.env");
        Console.WriteLine($"Project: {root}");
        var existing = EnvFile.ReadEnvValue(
            envPath,
            "EFFORTLESS_JWT");
        if (!string.IsNullOrEmpty(existing)
            && !JwtStore.IsJwtExpired(existing))
        {
            Console.WriteLine(
                "This project already has a valid EFFORTLESS_JWT.");
            Console.Write("Do you want to re-authenticate? (y/N): ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (response is not ("y" or "yes"))
            {
                Console.WriteLine(
                    "Project authentication cancelled.");
                return;
            }
        }

        var result = RunMagicLinkFlow();
        if (result is null)
        {
            return;
        }

        EnvFile.WriteEnvValue(
            envPath,
            "EFFORTLESS_JWT",
            result.Value.Token);
        Console.WriteLine(
            $"Project authentication successful! EFFORTLESS_JWT stored in {envPath}");
    }

    public void Subscription(string jwt)
    {
        if (string.IsNullOrEmpty(jwt))
        {
            Console.WriteLine(
                "You are not logged in. Use `effortless login` first.");
            return;
        }

        var result = Invoke(
            $"-p mode=viewPlan -p jwt={jwt}");
        if (result is null || !result.Value.Success)
        {
            Console.WriteLine(
                $"Failed to fetch subscription: {result?.Error ?? "No response from auth service"}");
            return;
        }

        var plan = result.Value.Plan ?? "free";
        var email = _jwtStore.GetStoredEmail()
                    ?? JwtStore.GetEmailFromJwt(jwt);
        CliLog.LogLine("Account:", email, ConsoleColor.Cyan);
        CliLog.LogLine(
            "Subscription plan:",
            plan,
            plan == "free"
                ? ConsoleColor.Yellow
                : ConsoleColor.Green);
        if (plan == "free")
        {
            Console.WriteLine(
                "Some tools may be unavailable on the free tier.");
            Console.WriteLine(
                $"You can upgrade your plan by logging in at bases.effortlessapi.com with email: {email}");
            Console.WriteLine(
                "and contacting us at https://bases.effortlessapi.com/dashboard/contact");
        }
    }

    public string GetPlan(string jwt)
    {
        if (string.IsNullOrEmpty(jwt))
        {
            return null;
        }

        var result = Invoke(
            $"-p mode=viewPlan -p jwt={jwt}");
        return result is { Success: true }
            ? result.Value.Plan ?? "free"
            : null;
    }

    private (string Token, string Email)? RunMagicLinkFlow()
    {
        Console.Write("Enter your email address: ");
        var email = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            Console.WriteLine(
                "Authentication cancelled - no email provided.");
            return null;
        }

        Console.WriteLine(
            $"Sending verification code to {email}...");
        var send = Invoke($"-p mode=auth -p email={email}");
        if (send is null || !send.Value.Success)
        {
            Console.WriteLine(
                $"Failed to send verification code: {send?.Error ?? "No response from auth service"}");
            return null;
        }

        Console.WriteLine("Verification code sent to your email.");
        Console.WriteLine();
        Console.Write("Enter the verification code you received: ");
        var code = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(code))
        {
            Console.WriteLine(
                "Authentication cancelled - no code provided.");
            return null;
        }

        Console.WriteLine("Verifying code...");
        var verify = Invoke(
            $"-p mode=verify -p email={email} -p code={code}");
        if (verify is null
            || !verify.Value.Success
            || string.IsNullOrEmpty(verify.Value.Token))
        {
            Console.WriteLine(
                $"Verification failed: {verify?.Error ?? "Invalid or expired code"}");
            return null;
        }

        return (verify.Value.Token, email);
    }

    private AuthResult? Invoke(string parameters)
    {
        var json = _bridge.InvokeAndGetOutput(parameters);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var value = JObject.Parse(json);
        return new AuthResult(
            value.GetValue(
                "Success",
                StringComparison.OrdinalIgnoreCase)?.Value<bool>() == true,
            value.GetValue(
                "Token",
                StringComparison.OrdinalIgnoreCase)?.Value<string>(),
            value.GetValue(
                "Plan",
                StringComparison.OrdinalIgnoreCase)?.Value<string>(),
            value.GetValue(
                "Error",
                StringComparison.OrdinalIgnoreCase)?.Value<string>());
    }

    private readonly record struct AuthResult(
        bool Success,
        string Token,
        string Plan,
        string Error);
}
