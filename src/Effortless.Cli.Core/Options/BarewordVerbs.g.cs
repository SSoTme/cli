using System.Collections.ObjectModel;

namespace Effortless.Cli.Options;

public static class BarewordVerbs
{
    public static IReadOnlyDictionary<string, Action<CliOptions>> Verbs { get; } =
        new ReadOnlyDictionary<string, Action<CliOptions>>(
            new Dictionary<string, Action<CliOptions>>(StringComparer.Ordinal)
            {
                ["help"] = options => options.help = true,
                ["init"] = options => options.init = true,
                ["install"] = options => options.install = true,
                ["uninstall"] = options => options.uninstall = true,
                ["build"] = options => options.build = true,
                ["rebuild"] = options => options.build = true,
                ["pull"] = options => options.build = true,
                ["buildall"] = options => options.buildAll = true,
                ["rebuildall"] = options => options.buildAll = true,
                ["pullAll"] = options => options.buildAll = true,
                ["buildlocal"] = options => options.buildLocal = true,
                ["list"] = options => options.describe = true,
                ["describe"] = options => options.describe = true,
                ["da"] = options => options.describeAll = true,
                ["describeall"] = options => options.describeAll = true,
                ["clean"] = options => options.clean = true,
                ["cleanall"] = options => options.cleanAll = true,
                ["cleanlocal"] = options => options.cleanLocal = true,
                ["auth"] = options => options.authenticate = true,
                ["login"] = options => options.authenticate = true,
                ["authenticate"] = options => options.authenticate = true,
                ["projectlogin"] = options => options.projectLogin = true,
                ["projectauth"] = options => options.projectLogin = true,
                ["logout"] = options => options.logout = true,
                ["signout"] = options => options.logout = true,
                ["info"] = options => options.info = true,
                ["version"] = options => options.version = true,
                ["v"] = options => options.version = true,
                ["dryRun"] = options => options.dryRun = true,
                ["listtoolurls"] = options => options.listUrls = true,
                ["listurls"] = options => options.listUrls = true,
                ["lu"] = options => options.listUrls = true,
                ["viewtoolurl"] = _ => { },
                ["viewurl"] = _ => { },
                ["vu"] = _ => { },
                ["vt"] = _ => { },
                ["settoolurl"] = _ => { },
                ["seturl"] = _ => { },
                ["su"] = _ => { },
                ["st"] = _ => { },
                ["removetoolurl"] = _ => { },
                ["removeurl"] = _ => { },
                ["ru"] = _ => { },
                ["rt"] = _ => { },
                ["refreshtools"] = options => options.refreshTools = true,
                ["listtools"] = options => options.listTools = true,
                ["tools"] = options => options.listTools = true,
                ["lt"] = options => options.listTools = true,
                ["searchtools"] = _ => { },
                ["search"] = _ => { },
                ["upgrade"] = options => options.upgrade = true,
                ["unpin"] = options => options.upgrade = true,
                ["subscription"] = options => options.subscription = true,
                ["plan"] = options => options.subscription = true,
                ["upgradeall"] = options => options.upgradeAll = true,
            });

    private static IReadOnlyDictionary<string, Action<CliOptions, string>> StringOptionSetters { get; } =
        new ReadOnlyDictionary<string, Action<CliOptions, string>>(
            new Dictionary<string, Action<CliOptions, string>>(StringComparer.Ordinal)
            {
                ["viewtoolurl"] = (options, value) => options.viewUrl = value ?? string.Empty,
                ["viewurl"] = (options, value) => options.viewUrl = value ?? string.Empty,
                ["vu"] = (options, value) => options.viewUrl = value ?? string.Empty,
                ["vt"] = (options, value) => options.viewUrl = value ?? string.Empty,
                ["settoolurl"] = (options, value) => options.setUrl = value,
                ["seturl"] = (options, value) => options.setUrl = value,
                ["su"] = (options, value) => options.setUrl = value,
                ["st"] = (options, value) => options.setUrl = value,
                ["removetoolurl"] = (options, value) => options.removeUrl = value,
                ["removeurl"] = (options, value) => options.removeUrl = value,
                ["ru"] = (options, value) => options.removeUrl = value,
                ["rt"] = (options, value) => options.removeUrl = value,
                ["searchtools"] = (options, value) => options.searchTools = value,
                ["search"] = (options, value) => options.searchTools = value,
            });

    public static bool TryApply(
        CliOptions options,
        IList<string> remainingArguments,
        out string transpiler)
    {
        transpiler = null;
        if (options is null || remainingArguments is null || remainingArguments.Count == 0)
        {
            return false;
        }

        var verb = $"{remainingArguments[0]}".ToLower();
        if (!Verbs.TryGetValue(verb, out var apply))
        {
            return false;
        }

        apply(options);
        transpiler = remainingArguments.Count > 1
            ? remainingArguments[1]
            : null;

        if (StringOptionSetters.TryGetValue(verb, out var setStringOption))
        {
            setStringOption(options, transpiler);
        }

        remainingArguments.RemoveAt(0);
        return true;
    }
}
