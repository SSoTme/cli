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
                ["listseeds"] = options => options.listSeeds = true,
                ["cloneseed"] = options => options.cloneSeed = true,
                ["clone"] = options => options.cloneSeed = true,
                ["auth"] = options => options.login = true,
                ["login"] = options => options.login = true,
                ["authenticate"] = options => options.login = true,
                ["projectlogin"] = options => options.projectLogin = true,
                ["projectauth"] = options => options.projectLogin = true,
                ["logout"] = options => options.logout = true,
                ["signout"] = options => options.logout = true,
                ["info"] = options => options.info = true,
                ["version"] = options => options.version = true,
                ["v"] = options => options.version = true,
                ["listtoolurls"] = options => options.listToolUrls = true,
                ["listurls"] = options => options.listToolUrls = true,
                ["lu"] = options => options.listToolUrls = true,
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
                ["subscription"] = options => options.plan = true,
                ["plan"] = options => options.plan = true,
                ["upgradeall"] = options => options.upgradeAll = true,
                ["buildwithsubprojects"] = options => options.buildWithSubprojects = true,
                ["cleanwithsubprojects"] = options => options.cleanWithSubprojects = true,
                ["describelocal"] = options => options.describeLocal = true,
                ["describewithsubprojects"] = options => options.describeWithSubprojects = true,
                ["disable"] = options => options.disable = true,
                ["enable"] = options => options.enable = true,
                ["serve"] = options => options.serve = true,
                ["listseedsources"] = options => options.listSeedSources = true,
                ["addseedsource"] = _ => { },
                ["removeseedsource"] = _ => { },
            });

    private static IReadOnlyDictionary<string, Action<CliOptions, string>> StringOptionSetters { get; } =
        new ReadOnlyDictionary<string, Action<CliOptions, string>>(
            new Dictionary<string, Action<CliOptions, string>>(StringComparer.Ordinal)
            {
                ["viewtoolurl"] = (options, value) => options.viewToolUrl = value ?? string.Empty,
                ["viewurl"] = (options, value) => options.viewToolUrl = value ?? string.Empty,
                ["vu"] = (options, value) => options.viewToolUrl = value ?? string.Empty,
                ["vt"] = (options, value) => options.viewToolUrl = value ?? string.Empty,
                ["settoolurl"] = (options, value) => options.setToolUrl = value,
                ["seturl"] = (options, value) => options.setToolUrl = value,
                ["su"] = (options, value) => options.setToolUrl = value,
                ["st"] = (options, value) => options.setToolUrl = value,
                ["removetoolurl"] = (options, value) => options.removeToolUrl = value,
                ["removeurl"] = (options, value) => options.removeToolUrl = value,
                ["ru"] = (options, value) => options.removeToolUrl = value,
                ["rt"] = (options, value) => options.removeToolUrl = value,
                ["searchtools"] = (options, value) => options.searchTools = value,
                ["search"] = (options, value) => options.searchTools = value,
                ["addseedsource"] = (options, value) => options.addSeedSource = value,
                ["removeseedsource"] = (options, value) => options.removeSeedSource = value,
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
