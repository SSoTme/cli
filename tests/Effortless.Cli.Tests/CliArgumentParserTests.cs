using Effortless.Cli.Options;

namespace Effortless.Cli.Tests;

public class CliArgumentParserTests
{
    [Fact]
    public void OptionsHaveLegacySafeDefaults()
    {
        var options = new CliOptions();

        Assert.Equal(180000, options.waitTimeout);
        Assert.Empty(options.input);
        Assert.Empty(options.parameters);
        Assert.Empty(options.addSetting);
        Assert.Empty(options.removeSetting);
    }

    [Theory]
    [InlineData("build")]
    [InlineData("-build")]
    [InlineData("--build")]
    public void ReservedBuildFormsSelectSameOption(string form)
    {
        var invocation = new CliArgumentParser().Parse(new[] { form });

        Assert.False(invocation.HasErrors, invocation.ErrorText);
        Assert.True(invocation.Options.build);
    }

    [Theory]
    [InlineData("build")]
    [InlineData("-build")]
    [InlineData("--build")]
    public void CommandLineReservedBuildFormsSelectSameOption(string form)
    {
        var invocation = new CliArgumentParser().Parse(form);

        Assert.False(invocation.HasErrors, invocation.ErrorText);
        Assert.True(invocation.Options.build);
    }

    [Fact]
    public void CommandLineStringUsesPlossumQuoting()
    {
        var invocation = new CliArgumentParser()
            .Parse("sample-tool -p \"description=hello world\"");

        Assert.False(invocation.HasErrors, invocation.ErrorText);
        Assert.Equal(new[] { "description=hello world" }, invocation.Options.parameters);
        Assert.Equal(new[] { "sample-tool" }, invocation.RemainingArguments);
    }

    [Fact]
    public void ArgvPreservesAnArgumentContainingSpaces()
    {
        var invocation = new CliArgumentParser()
            .Parse(new[] { "sample-tool", "-p", "description=hello world" });

        Assert.False(invocation.HasErrors, invocation.ErrorText);
        Assert.Equal(new[] { "description=hello world" }, invocation.Options.parameters);
        Assert.Equal(new[] { "sample-tool" }, invocation.RemainingArguments);
    }

    [Fact]
    public void BarewordStringOptionCopiesNextArgument()
    {
        var invocation = new CliArgumentParser()
            .Parse(new[] { "searchTools", "rulebook-to-sql" });

        Assert.False(invocation.HasErrors, invocation.ErrorText);
        Assert.Equal("rulebook-to-sql", invocation.Options.searchTools);
        Assert.Equal("rulebook-to-sql", invocation.Transpiler);
        Assert.Equal(new[] { "rulebook-to-sql" }, invocation.RemainingArguments);
    }

    [Fact(DisplayName = "unit-bareword-verbs: bareword parsing preserves legacy matching semantics")]
    public void LegacyMixedCaseBarewordsRemainUnmatchable()
    {
        var pullAll = new CliArgumentParser().Parse(new[] { "pullAll" });

        Assert.False(pullAll.Options.buildAll);
        Assert.Equal(new[] { "pullAll" }, pullAll.RemainingArguments);

        // D27: -dryRun is gone entirely, so the bareword is just an unknown
        // tool name rather than a case-sensitivity quirk.
        var dryRun = new CliArgumentParser().Parse(new[] { "dryRun" });
        Assert.Equal(new[] { "dryRun" }, dryRun.RemainingArguments);
    }

    [Fact]
    public void D11NoDashCommandIsReservedBeforeToolArgument()
    {
        var invocation = new CliArgumentParser()
            .Parse(new[] { "listVersions", "sample-tool" });

        Assert.False(invocation.HasErrors, invocation.ErrorText);
        Assert.True(invocation.Options.listVersions);
        Assert.Equal(new[] { "sample-tool" }, invocation.RemainingArguments);
    }

    [Fact]
    public void CommandLineNoDashCommandPreservesQuotedToolArgument()
    {
        var invocation = new CliArgumentParser()
            .Parse("listVersions \"sample tool\"");

        Assert.False(invocation.HasErrors, invocation.ErrorText);
        Assert.True(invocation.Options.listVersions);
        Assert.Equal(new[] { "sample tool" }, invocation.RemainingArguments);
    }

    [Theory]
    [InlineData("execute")]
    [InlineData("-execute")]
    [InlineData("--execute")]
    public void CommandLineValueCommandFormsReportMissingValue(string form)
    {
        var invocation = new CliArgumentParser().Parse(form);

        Assert.True(invocation.HasErrors);
        Assert.Equal(-1, invocation.ParseResult);
    }

    [Fact]
    public void DoubleDashModifierIsNormalized()
    {
        var invocation = new CliArgumentParser()
            .Parse(new[] { "sample-tool", "--waitTimeout=42" });

        Assert.False(invocation.HasErrors, invocation.ErrorText);
        Assert.Equal(42, invocation.Options.waitTimeout);
        Assert.Equal(new[] { "sample-tool" }, invocation.RemainingArguments);
    }

    [Fact]
    public void LtAliasListsRemoteCatalogTools()
    {
        var invocation = new CliArgumentParser().Parse(new[] { "-lt" });

        Assert.False(invocation.HasErrors, invocation.ErrorText);
        Assert.True(invocation.Options.listTools);
        Assert.False(invocation.Options.listToolUrls);
    }

    [Fact]
    public void SeedVerbsAndTriggerOptionAreReserved()
    {
        var list = new CliArgumentParser()
            .Parse(new[] { "listSeeds", "example" });
        var clone = new CliArgumentParser()
            .Parse(new[] { "clone", "seed-api", "my-api" });
        var trigger = new CliArgumentParser()
            .Parse(
                new[]
                {
                    "build",
                    "-buildOnTrigger",
                    "app123",
                });

        Assert.True(list.Options.listSeeds);
        Assert.Equal(
            ["example"],
            list.RemainingArguments);
        Assert.True(clone.Options.cloneSeed);
        Assert.Equal(
            ["seed-api", "my-api"],
            clone.RemainingArguments);
        Assert.True(trigger.Options.build);
        Assert.Equal(
            "app123",
            trigger.Options.buildOnTrigger);
    }
}
