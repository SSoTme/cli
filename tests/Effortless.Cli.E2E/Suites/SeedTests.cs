using System.Text.Json.Nodes;
using Effortless.Cli.E2E.Harness;

namespace Effortless.Cli.E2E.Suites;

/// <summary>
/// Step 14: seed sources, listSeeds across sources, cloneSeed forms, and the
/// $key$ replacement contract, against a mock GitHub.
/// </summary>
public sealed class SeedTests
{
    private const string SeedProjectJson = """
        {
          "Name": "acme-orders",
          "ProjectSettings": [],
          "ProjectTranspilers": [
            {
              "Name": "never-run",
              "RelativePath": "",
              "CommandLine": "-execute \"echo ran > ran.txt\""
            }
          ]
        }
        """;

    [Fact(DisplayName = "seed-sources-default-list: no seed_sources.json means the two defaults")]
    public async Task DefaultSourcesAreListed()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        sandbox.SeedEmptyHome();

        var plain = await cli.Run(["listSeedSources"], sandbox.ProjectPath, sandbox);
        var withEnv = await cli.Run(
            ["-listSeedSources"],
            sandbox.ProjectPath,
            sandbox,
            environment: new Dictionary<string, string> { ["EFFORTLESS_SEED_GITHUB_ACCOUNT"] = "extra" });

        Assert.Equal(0, plain.ExitCode);
        Assert.Equal(
            Lines("Seed sources (searched in order):", "  ssotme (default)", "  effortlessapi (default)"),
            plain.Stdout);
        Assert.Equal(0, withEnv.ExitCode);
        Assert.Equal(
            Lines("Seed sources (searched in order):", "  extra (env)", "  ssotme (default)", "  effortlessapi (default)"),
            withEnv.Stdout);
        Assert.False(File.Exists(SourcesPath(sandbox)));
    }

    [Fact(DisplayName = "seed-source-add: addSeedSource creates the file from the defaults and appends")]
    public async Task AddSourceCreatesFileFromDefaults()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        sandbox.SeedEmptyHome();

        var first = await cli.Run(["addSeedSource", "acme"], sandbox.ProjectPath, sandbox);
        var again = await cli.Run(["-addSeedSource", "acme"], sandbox.ProjectPath, sandbox);
        var invalid = await cli.Run(["addSeedSource", "bad/name"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, first.ExitCode);
        Assert.Contains("Added seed source 'acme'.", first.Stdout, StringComparison.Ordinal);
        Assert.Contains(Lines("  ssotme", "  effortlessapi", "  acme"), first.Stdout, StringComparison.Ordinal);
        Assert.Equal(["ssotme", "effortlessapi", "acme"], ReadSources(sandbox));
        Assert.Equal(0, again.ExitCode);
        Assert.Contains("Seed source 'acme' is already listed.", again.Stdout, StringComparison.Ordinal);
        Assert.Equal(["ssotme", "effortlessapi", "acme"], ReadSources(sandbox));
        Assert.True(invalid.Failed);
        Assert.Contains("ERROR: 'bad/name' is not a valid GitHub account name.", invalid.Stdout, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "seed-source-remove-default: a default can be removed and stays removed")]
    public async Task RemovingADefaultPersists()
    {
        var cli = new CliUnderTest();
        using var sandbox = Sandbox.Create(cli);
        sandbox.SeedEmptyHome();

        var remove = await cli.Run(["removeSeedSource", "ssotme"], sandbox.ProjectPath, sandbox);
        var list = await cli.Run(["listSeedSources"], sandbox.ProjectPath, sandbox);
        var missing = await cli.Run(["removeSeedSource", "nope"], sandbox.ProjectPath, sandbox);

        Assert.Equal(0, remove.ExitCode);
        Assert.Contains("Removed seed source 'ssotme'.", remove.Stdout, StringComparison.Ordinal);
        Assert.Equal(["effortlessapi"], ReadSources(sandbox));
        Assert.Equal(Lines("Seed sources (searched in order):", "  effortlessapi"), list.Stdout);
        Assert.True(missing.Failed);
        Assert.Contains("ERROR: Seed source 'nope' is not listed. Current sources: effortlessapi", missing.Stdout, StringComparison.Ordinal);
        Assert.Equal(["effortlessapi"], ReadSources(sandbox));
    }

    [Fact(DisplayName = "list-seeds-across-sources: listSeeds walks every source in order and groups by account")]
    public async Task ListSeedsGroupsByAccount()
    {
        var cli = new CliUnderTest();
        await using var github = new MockGitHubServer();
        using var sandbox = SeedTwoAccounts(cli, github);

        var all = await cli.Run(["listSeeds"], sandbox.ProjectPath, sandbox, environment: github.Environment);
        var beta = await cli.Run(["-listSeeds", "beta"], sandbox.ProjectPath, sandbox, environment: github.Environment);

        Assert.Equal(0, all.ExitCode);
        Assert.Equal(
            Lines(
                "Effortless seeds from GitHub account 'acme' (2):",
                "  acme-orders  Orders starter",
                "  shared-seed  Shared by two accounts",
                "Effortless seeds from GitHub account 'beta' (2):",
                "  beta-crm  CRM starter",
                "  shared-seed  Beta's copy"),
            all.Stdout);
        Assert.Equal(0, beta.ExitCode);
        Assert.Equal(
            Lines(
                "Effortless seeds from GitHub account 'beta' (2):",
                "  beta-crm  CRM starter",
                "  shared-seed  Beta's copy"),
            beta.Stdout);
    }

    [Fact(DisplayName = "clone-seed-qualified: cloneSeed account/repo clones exactly that repository")]
    public async Task QualifiedCloneUsesThatAccount()
    {
        var cli = new CliUnderTest();
        await using var github = new MockGitHubServer();
        using var sandbox = SeedTwoAccounts(cli, github);

        var result = await cli.Run(["cloneSeed", "acme/acme-orders"], sandbox.ProjectPath, sandbox, environment: github.Environment);

        Assert.Equal(0, result.ExitCode);
        var clone = Path.Combine(sandbox.ProjectPath, "acme-orders");
        Assert.True(Directory.Exists(Path.Combine(clone, ".git")));
        Assert.True(File.Exists(Path.Combine(clone, "effortless.json")));
        // macOS reports /var/folders as /private/var/folders once resolved, so match the suffix.
        Assert.Matches("Cloned Effortless seed acme/acme-orders to .*acme-orders", result.Stdout);
        Assert.Contains("effortless build` when you are ready", result.Stdout, StringComparison.Ordinal);
        Assert.Equal(["acme"], github.ApiAccountsQueried.Distinct());
    }

    [Fact(DisplayName = "clone-seed-bare-first-match: a bare name found in one source clones it and says which")]
    public async Task BareNameFoundInOneSource()
    {
        var cli = new CliUnderTest();
        await using var github = new MockGitHubServer();
        using var sandbox = SeedTwoAccounts(cli, github);

        var result = await cli.Run(["cloneSeed", "beta-crm"], sandbox.ProjectPath, sandbox, environment: github.Environment);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Found 'beta-crm' in seed source 'beta'.", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Cloned Effortless seed beta/beta-crm to ", result.Stdout, StringComparison.Ordinal);
        Assert.True(Directory.Exists(Path.Combine(sandbox.ProjectPath, "beta-crm", ".git")));
    }

    [Fact(DisplayName = "clone-seed-bare-ambiguous: a bare name present in two sources is an error listing both")]
    public async Task BareNameInTwoSourcesIsAmbiguous()
    {
        var cli = new CliUnderTest();
        await using var github = new MockGitHubServer();
        using var sandbox = SeedTwoAccounts(cli, github);

        var ambiguous = await cli.Run(["cloneSeed", "shared-seed"], sandbox.ProjectPath, sandbox, environment: github.Environment);
        var missing = await cli.Run(["cloneSeed", "nothing-here"], sandbox.ProjectPath, sandbox, environment: github.Environment);

        Assert.True(ambiguous.Failed);
        Assert.Contains(
            "Seed 'shared-seed' is ambiguous: acme/shared-seed, beta/shared-seed. Use account/repo.",
            ambiguous.Stdout,
            StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(sandbox.ProjectPath, "shared-seed")));
        Assert.True(missing.Failed);
        Assert.Contains(
            "Seed 'nothing-here' was not found in any seed source (acme, beta).",
            missing.Stdout,
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "clone-seed-preserves-git-and-runs-nothing: cloning keeps history and executes nothing")]
    public async Task CloneRunsNothing()
    {
        var cli = new CliUnderTest();
        await using var github = new MockGitHubServer();
        using var sandbox = SeedTwoAccounts(cli, github);

        var result = await cli.Run(["cloneSeed", "acme/acme-orders", "here"], sandbox.ProjectPath, sandbox, environment: github.Environment);

        Assert.Equal(0, result.ExitCode);
        var clone = Path.Combine(sandbox.ProjectPath, "here");
        Assert.True(File.Exists(Path.Combine(clone, ".git", "HEAD")));
        Assert.False(File.Exists(Path.Combine(clone, "ran.txt")));
        Assert.False(Directory.Exists(Path.Combine(clone, ".effortless")));
        Assert.True(File.Exists(Path.Combine(clone, "$project-name$.md")), "replacement must not run at clone time");
        Assert.Matches("Run `cd \".*here\" && effortless build` when you are ready to execute its pipeline", result.Stdout);
    }

    [Fact(DisplayName = "seed-replacements-effortless-seed-json: $key$ replacement runs on first project load using defaults")]
    public async Task ReplacementsRunOnFirstLoad()
    {
        var cli = new CliUnderTest();
        await using var github = new MockGitHubServer();
        using var sandbox = SeedTwoAccounts(cli, github);
        var clone = Path.Combine(sandbox.ProjectPath, "acme-orders");
        var cloned = await cli.Run(["cloneSeed", "acme/acme-orders"], sandbox.ProjectPath, sandbox, environment: github.Environment);
        Assert.Equal(0, cloned.ExitCode);

        var describe = await cli.Run(["describe"], clone, sandbox);

        Assert.Equal(0, describe.ExitCode);
        Assert.False(File.Exists(Path.Combine(clone, "$project-name$.md")));
        Assert.Equal("# orders\n", File.ReadAllText(Path.Combine(clone, "orders.md")).Replace("\r\n", "\n"));
        // A defaulted value is applied without prompting and without being recorded;
        // only prompted answers are written to seed-config-values.json.
        Assert.False(File.Exists(Path.Combine(clone, "seed-config-values.json")));
        Assert.True(File.Exists(Path.Combine(clone, "effortless-seed.json")), "the template itself is never rewritten");
    }

    private static Sandbox SeedTwoAccounts(CliUnderTest cli, MockGitHubServer github)
    {
        var sandbox = Sandbox.Create(cli);
        sandbox.SeedEmptyHome();
        sandbox.WriteHomeFile(".effortless/seed_sources.json", """{ "sources": ["acme", "beta"] }""");
        github.AddRepository(
            "acme",
            "acme-orders",
            "Orders starter",
            new Dictionary<string, string>
            {
                ["effortless.json"] = SeedProjectJson,
                ["README.md"] = "# acme orders\n",
                ["effortless-seed.json"] = """
                    { "replacements": [ { "key": "project-name", "description": "Project name", "default": "orders" } ] }
                    """,
                ["$project-name$.md"] = "# $project-name$\n",
            });
        github.AddRepository(
            "acme",
            "shared-seed",
            "Shared by two accounts",
            new Dictionary<string, string> { ["effortless.json"] = SeedProjectJson });
        github.AddRepository(
            "acme",
            "plain-repo",
            "Not a seed",
            new Dictionary<string, string> { ["README.md"] = "no effortless.json here\n" });
        github.AddRepository(
            "beta",
            "beta-crm",
            "CRM starter",
            new Dictionary<string, string> { ["effortless.json"] = SeedProjectJson });
        github.AddRepository(
            "beta",
            "shared-seed",
            "Beta's copy",
            new Dictionary<string, string> { ["effortless.json"] = SeedProjectJson });
        return sandbox;
    }

    private static string SourcesPath(Sandbox sandbox) =>
        Path.Combine(sandbox.HomePath, ".effortless", "seed_sources.json");

    private static string[] ReadSources(Sandbox sandbox) =>
        JsonNode.Parse(File.ReadAllText(SourcesPath(sandbox)))!["sources"]!.AsArray()
            .Select(node => node!.GetValue<string>())
            .ToArray();

    private static string Lines(params string[] lines) =>
        string.Concat(lines.Select(line => line + Environment.NewLine));
}
