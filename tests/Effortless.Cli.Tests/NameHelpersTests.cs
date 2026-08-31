using Effortless.Cli.Text;

namespace Effortless.Cli.Tests;

public sealed class NameHelpersTests
{
    [Fact(DisplayName = "unit-sanitize-url: SanitizeUrlForFilename matches legacy keys")]
    public void SanitizeUrlForFilenameMatchesLegacyKeys()
    {
        Assert.Equal(
            "httpsexamplecomsome-pathx1y2",
            NameHelpers.SanitizeUrlForFilename("https://Example.COM/some path?x=1&y=2"));
        Assert.Equal(string.Empty, NameHelpers.SanitizeUrlForFilename(null));
    }

    [Fact(DisplayName = "unit-lower-hyphen-name: naming helpers preserve legacy behavior")]
    public void NamingHelpersPreserveLegacyBehavior()
    {
        Assert.Equal("to-uppercase", NameHelpers.LowerHyphenName("ToUppercase"));
        Assert.Equal("to-uppercase", NameHelpers.LowerHyphenName("to-uppercase"));
        Assert.Equal("j-s-o-n-to-handlebars", NameHelpers.LowerHyphenName("JSON To Handlebars"));
        Assert.Equal("rulebooktox", NameHelpers.LowerHyphenName("rulebook_to_x"));

        Assert.Equal("ToUppercase", NameHelpers.ToCamelString("to-uppercase"));
        Assert.Equal("To Uppercase", NameHelpers.ToTitle("to-uppercase"));
        Assert.Equal("To Uppercase", NameHelpers.TitleFromCamel("ToUppercase"));
        Assert.Equal("Hello World", NameHelpers.ToTitleCase("helloWorld"));
    }
}
