namespace Effortless.Cli.Auth;

public sealed class MagicLinkAuth
{
    public const string LoginUnavailableMessage =
        "EffortlessAPI authentication is not available yet. " +
        "The login command is reserved for future magic-link authentication; " +
        "tools and buildOnTrigger currently run without login.";

    public const string ProjectLoginUnavailableMessage =
        "EffortlessAPI project authentication is not available yet. " +
        "The projectlogin command is reserved for future magic-link authentication; " +
        "the project was not modified.";

    public const string SubscriptionUnavailableMessage =
        "EffortlessAPI subscription lookup is not available yet because " +
        "authentication is not enabled.";

    public int Login()
    {
        Console.Error.WriteLine(LoginUnavailableMessage);
        return -1;
    }

    public int ProjectLogin()
    {
        Console.Error.WriteLine(ProjectLoginUnavailableMessage);
        return -1;
    }

    public int Subscription(string jwt)
    {
        _ = jwt;
        Console.Error.WriteLine(SubscriptionUnavailableMessage);
        return -1;
    }

    public string GetPlan(string jwt)
    {
        _ = jwt;
        return null;
    }
}
