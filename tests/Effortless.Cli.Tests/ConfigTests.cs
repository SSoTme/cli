using System.Text;
using Effortless.Cli.Config;
using Newtonsoft.Json;

namespace Effortless.Cli.Tests;

public sealed class ConfigTests
{
    [Fact(DisplayName = "unit-env-file: EnvFile parsing and account suffix precedence match legacy")]
    public void EnvFileParsingAndAccountSuffixPrecedenceMatchLegacy()
    {
        using var directory = new TestDirectory();
        File.WriteAllText(
            directory.File("effortless.env"),
            """
            # ignored
            AIRTABLE_PAT="first"
            airtable_api_key='second'
            AirTable_Base_Id = app123
            OTHER=value=with=equals
            malformed
            """);

        var env = Assert.IsType<EnvFile>(EnvFile.LoadFrom(directory.Path));

        Assert.Equal("first", env.GetValue("airtable_pat"));
        Assert.Equal("value=with=equals", env.GetValue("OTHER"));
        var account = env.ResolveAccountParams("airtable");
        Assert.Equal("first", account["apiKey"]);
        Assert.Equal("app123", account["baseId"]);
    }

    [Fact(DisplayName = "unit-env-write: environment values replace, append, and create")]
    public void EnvironmentValuesReplaceAppendAndCreate()
    {
        using var directory = new TestDirectory();
        var existing = directory.File("existing.env");
        var created = directory.File("created.env");
        File.WriteAllText(
            existing,
            $"# retained{Environment.NewLine}API_KEY=old{Environment.NewLine}");

        EnvFile.WriteEnvValue(existing, "api_key", "new");
        EnvFile.WriteEnvValue(existing, "BASE_ID", "app123");
        EnvFile.WriteEnvValue(created, "TOKEN", "secret");

        Assert.Equal(
            [
                "# retained",
                "api_key=new",
                "BASE_ID=app123",
            ],
            File.ReadAllLines(existing));
        Assert.Equal(["TOKEN=secret"], File.ReadAllLines(created));
    }

    [Fact(DisplayName = "unit-jwt-helpers: JWT helpers match legacy behavior")]
    public void JwtHelpersMatchLegacyBehavior()
    {
        var valid = Jwt(
            new
            {
                email = "owner@example.com",
                exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            });
        var expired = Jwt(
            new
            {
                email = "old@example.com",
                exp = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds(),
            });

        Assert.Equal("owner@example.com", JwtStore.GetEmailFromJwt(valid));
        Assert.False(JwtStore.IsJwtExpired(valid));
        Assert.True(JwtStore.IsJwtExpired(expired));
        Assert.Null(JwtStore.GetEmailFromJwt("not-a-jwt"));
        Assert.True(JwtStore.IsJwtExpired("not-a-jwt"));
        Assert.True(JwtStore.IsJwtExpired(Jwt(new { email = "missing-exp@example.com" })));
    }

    private static string Jwt(object payload)
    {
        static string Encode(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=');

        return $"{Encode("{}")}.{Encode(JsonConvert.SerializeObject(payload))}.signature";
    }
}
