using System.Text;
using System.Text.Json;
using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

public sealed class AuthTests
{
    private const string Email = "a@b.c";
    private const string ExistingEmail = "x@y.z";

    [Fact(DisplayName = "auth-login-flow: login via mock bridge")]
    public async Task LoginUsesTheAuthBridgeOrPinsLegacyShortCircuit()
    {
        var cli = new CliUnderTest();
        await using var bridge = CreateBridge();
        using var sandbox = Sandbox.Create(cli);
        bridge.SeedHome(sandbox);

        var result = await cli.Run(
            ["login"],
            sandbox.ProjectPath,
            sandbox,
            stdin: $"{Email}{Environment.NewLine}123456{Environment.NewLine}");

        var tokenPath = HomeConfigPath(sandbox, "effortlessapi_token.txt");
        var infoPath = HomeConfigPath(sandbox, "effortlessapi_token_info.json");
        if (Behavior.IsLegacy)
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("=== EffortlessAPI Authentication ===", result.Stdout, StringComparison.Ordinal);
            Assert.Contains($"Sending verification code to {Email}...", result.Stdout, StringComparison.Ordinal);
            Assert.Contains(
                "Failed to send verification code: No response from auth service",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.Empty(bridge.Requests);
            Assert.False(File.Exists(tokenPath));
            Assert.False(File.Exists(infoPath));
        }
        else
        {
            Assert.True(result.Failed);
            Assert.Contains(
                "EffortlessAPI authentication is not available yet.",
                result.Stderr,
                StringComparison.Ordinal);
            Assert.Empty(bridge.Requests);
            Assert.False(File.Exists(tokenPath));
            Assert.False(File.Exists(infoPath));
        }

        bridge.ThrowIfFaulted();
    }

    [Fact(DisplayName = "auth-login-already: login when already authenticated")]
    public async Task LoginCanBeCancelledWhenAlreadyAuthenticated()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        var token = CreateJwt(ExistingEmail);
        WriteGlobalToken(sandbox, token, ExistingEmail);

        var result = await cli.Run(
            ["login"],
            sandbox.ProjectPath,
            sandbox,
            stdin: $"n{Environment.NewLine}");

        if (Behavior.IsLegacy)
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                $"You are already authenticated as {ExistingEmail}.",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.Contains(
                "Do you want to re-authenticate? (y/N): ",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.Contains("Authentication cancelled.", result.Stdout, StringComparison.Ordinal);
        }
        else
        {
            Assert.True(result.Failed);
            Assert.Contains(
                "EffortlessAPI authentication is not available yet.",
                result.Stderr,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Do you want to re-authenticate?",
                result.Combined,
                StringComparison.Ordinal);
        }
        Assert.Equal(token, File.ReadAllText(HomeConfigPath(sandbox, "effortlessapi_token.txt")));
    }

    [Fact(DisplayName = "auth-project-login: projectLogin writes the env file")]
    public async Task ProjectLoginUsesTheAuthBridgeOrPinsLegacyShortCircuit()
    {
        var cli = new CliUnderTest();
        await using var bridge = CreateBridge();
        using var sandbox = Sandbox.Create(cli);
        WriteMinimalProject(sandbox);
        sandbox.WriteFile("effortless.env", $"EXISTING_KEY=keep{Environment.NewLine}");
        bridge.SeedHome(sandbox);

        var result = await cli.Run(
            ["projectlogin"],
            sandbox.ProjectPath,
            sandbox,
            stdin: $"{Email}{Environment.NewLine}123456{Environment.NewLine}");

        var env = sandbox.ReadFile("effortless.env");
        Assert.Contains("EXISTING_KEY=keep", env, StringComparison.Ordinal);

        if (Behavior.IsLegacy)
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("=== Project Authentication ===", result.Stdout, StringComparison.Ordinal);
            Assert.Contains($"Sending verification code to {Email}...", result.Stdout, StringComparison.Ordinal);
            Assert.Contains(
                "Failed to send verification code: No response from auth service",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.DoesNotContain("EFFORTLESS_JWT=", env, StringComparison.Ordinal);
            Assert.Empty(bridge.Requests);
        }
        else
        {
            Assert.True(result.Failed);
            Assert.Contains(
                "EffortlessAPI project authentication is not available yet.",
                result.Stderr,
                StringComparison.Ordinal);
            Assert.DoesNotContain("EFFORTLESS_JWT=", env, StringComparison.Ordinal);
            Assert.Empty(bridge.Requests);
        }

        bridge.ThrowIfFaulted();
    }

    [Fact(DisplayName = "auth-logout: logout")]
    public async Task LogoutConfirmsDeletesCancelsAndHandlesNoSession()
    {
        var cli = new CliUnderTest();

        using (var confirmed = Sandbox.Create(cli))
        {
            WriteMinimalProject(confirmed);
            WriteGlobalToken(confirmed, CreateJwt(ExistingEmail), ExistingEmail);

            var result = await cli.Run(
                ["logout"],
                confirmed.ProjectPath,
                confirmed,
                stdin: $"y{Environment.NewLine}");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"You're logged in as {ExistingEmail}.", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("Logged out successfully.", result.Stdout, StringComparison.Ordinal);
            Assert.False(File.Exists(HomeConfigPath(confirmed, "effortlessapi_token.txt")));
            Assert.False(File.Exists(HomeConfigPath(confirmed, "effortlessapi_token_info.json")));
        }

        using (var cancelled = Sandbox.Create(cli))
        {
            WriteMinimalProject(cancelled);
            var token = CreateJwt(ExistingEmail);
            WriteGlobalToken(cancelled, token, ExistingEmail);

            var result = await cli.Run(
                ["logout"],
                cancelled.ProjectPath,
                cancelled,
                stdin: $"n{Environment.NewLine}");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Logout cancelled.", result.Stdout, StringComparison.Ordinal);
            Assert.Equal(
                token,
                File.ReadAllText(HomeConfigPath(cancelled, "effortlessapi_token.txt")));
        }

        using (var noSession = Sandbox.Create(cli))
        {
            WriteMinimalProject(noSession);

            var result = await cli.Run(["logout"], noSession.ProjectPath, noSession);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("No active session found.", result.Stdout, StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "auth-subscription-no-token: subscription without login")]
    public async Task SubscriptionWithoutTokenReportsLoginRequirement()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        WriteMinimalProject(sandbox);

        foreach (var argument in new[] { "plan", "-subscription" })
        {
            var result = await cli.Run([argument], sandbox.ProjectPath, sandbox);

            if (Behavior.IsLegacy)
            {
                Assert.Equal(0, result.ExitCode);
                Assert.Contains(
                    "You are not logged in. Use `effortless login` first.",
                    result.Stdout,
                    StringComparison.Ordinal);
            }
            else
            {
                Assert.True(result.Failed);
                Assert.Contains(
                    "EffortlessAPI subscription lookup is not available yet",
                    result.Stderr,
                    StringComparison.Ordinal);
            }
        }
    }

    [Fact(DisplayName = "auth-subscription: subscription with a token")]
    public async Task SubscriptionUsesTheAuthBridgeOrPinsLegacyShortCircuit()
    {
        var cli = new CliUnderTest();
        await using var bridge = CreateBridge();
        using var sandbox = Sandbox.Create(cli);
        WriteMinimalProject(sandbox);
        var token = CreateJwt(ExistingEmail);
        WriteGlobalToken(sandbox, token, ExistingEmail);
        bridge.SeedHome(sandbox);

        var result = await cli.Run(["plan"], sandbox.ProjectPath, sandbox);

        if (Behavior.IsLegacy)
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                "Failed to fetch subscription: No response from auth service",
                result.Stdout,
                StringComparison.Ordinal);
            Assert.Empty(bridge.Requests);
        }
        else
        {
            Assert.True(result.Failed);
            Assert.Contains(
                "EffortlessAPI subscription lookup is not available yet",
                result.Stderr,
                StringComparison.Ordinal);
            Assert.Empty(bridge.Requests);
        }

        bridge.ThrowIfFaulted();
    }

    [Fact(DisplayName = "auth-set-api-key: setAccountAPIKey")]
    public async Task SetAccountApiKeyWritesReplacesAndRejectsBadSyntax()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);

        var initial = await cli.Run(
            ["-setAccountAPIKey", "acme/k1"],
            sandbox.ProjectPath,
            sandbox);
        var replacement = await cli.Run(
            ["-api", "acme=k2"],
            sandbox.ProjectPath,
            sandbox);
        var baserow = await cli.Run(
            ["-api", "baserow/u/p"],
            sandbox.ProjectPath,
            sandbox);
        var invalid = await cli.Run(["-api", "bad"], sandbox.ProjectPath, sandbox);
        var invalidBaserow = await cli.Run(
            ["-api", "baserow/u"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, initial.ExitCode);
        Assert.Equal(0, replacement.ExitCode);
        Assert.Equal(0, baserow.ExitCode);
        Assert.True(invalid.Failed);
        Assert.Contains(
            "Default Syntax: -setAccountAPIKey=account/KEY",
            invalid.Stdout,
            StringComparison.Ordinal);
        Assert.True(invalidBaserow.Failed);
        Assert.Contains(
            "Syntax for \"baserow\": -setAccountAPIKey=baserow/username/password",
            invalidBaserow.Stdout,
            StringComparison.Ordinal);

        var keyPath = HomeConfigPath(sandbox, "ssotme.key");
        using var key = JsonDocument.Parse(File.ReadAllText(keyPath));
        var apiKeys = key.RootElement.GetProperty("APIKeys");
        Assert.Equal("k2", apiKeys.GetProperty("acme").GetString());
        using var baserowValue = JsonDocument.Parse(
            apiKeys.GetProperty("baserow").GetString()
            ?? throw new InvalidDataException("The baserow key was JSON null."));
        Assert.Equal("u", baserowValue.RootElement.GetProperty("username").GetString());
        Assert.Equal("p", baserowValue.RootElement.GetProperty("password").GetString());
        AssertSecretFileMode(keyPath);
    }

    [Fact(DisplayName = "auth-set-api-key-runas: setAccountAPIKey -runAs")]
    public async Task SetAccountApiKeyRunAsWritesOnlyNamedKeyFile()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);

        var result = await cli.Run(
            ["-api", "acme/k", "-runAs", "bob"],
            sandbox.ProjectPath,
            sandbox);

        Assert.Equal(0, result.ExitCode);
        var namedPath = HomeConfigPath(sandbox, "ssotme.bob.key");
        Assert.True(File.Exists(namedPath));
        Assert.False(File.Exists(HomeConfigPath(sandbox, "ssotme.key")));
        using var key = JsonDocument.Parse(File.ReadAllText(namedPath));
        Assert.Equal(
            "k",
            key.RootElement.GetProperty("APIKeys").GetProperty("acme").GetString());
        AssertSecretFileMode(namedPath);
    }

    [Fact(DisplayName = "auth-key-permissions: the config dir and key file are locked down")]
    public async Task ConfigDirAndKeyFileAreLockedDown()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);

        var result = await cli.Run(["-api", "acme/k"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, result.ExitCode);
        var configDir = Path.Combine(sandbox.HomePath, ".ssotme");
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(configDir));
        AssertSecretFileMode(HomeConfigPath(sandbox, "ssotme.key"));
    }

    private static AuthTestBridge CreateBridge()
    {
        var bridge = new AuthTestBridge();
        bridge.Responses["auth"] = """{"Success":true}""";
        bridge.Responses["verify"] =
            JsonSerializer.Serialize(new { Success = true, Token = CreateJwt(Email) });
        bridge.Responses["viewPlan"] = """{"Success":true,"Plan":"free"}""";
        return bridge;
    }

    private static void WriteMinimalProject(Sandbox sandbox)
    {
        sandbox.WriteFile(
            "effortless.json",
            """
            {
              "Name": "auth-characterization",
              "ProjectSettings": [],
              "ProjectTranspilers": []
            }
            """);
    }

    private static void WriteGlobalToken(Sandbox sandbox, string token, string email)
    {
        sandbox.WriteHomeFile(".ssotme/effortlessapi_token.txt", token);
        sandbox.WriteHomeFile(
            ".ssotme/effortlessapi_token_info.json",
            JsonSerializer.Serialize(new { Token = token, Email = email }));
    }

    private static string HomeConfigPath(Sandbox sandbox, string fileName) =>
        Path.Combine(sandbox.HomePath, ".ssotme", fileName);

    private static string CreateJwt(string email)
    {
        var header = Base64Url("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64Url(JsonSerializer.Serialize(new { email, exp = 4_102_444_800L }));
        return $"{header}.{payload}.";
    }

    private static string Base64Url(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static void AssertSecretFileMode(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(path));
    }
}
