using Effortless.Cli.Options;

namespace Effortless.Cli.Commands;

/// <summary>
/// Step 09 §4: -help is grouped and filterable instead of one flat list.
/// The default prints only <c>primary</c>-tier commands grouped by category;
/// <c>-help &lt;category&gt;</c> prints one category at every tier;
/// <c>-help &lt;option&gt;</c> prints that option's detail; <c>-help all</c> is
/// the flat dump. All of it reads <see cref="CliOptionMetadata"/>, generated
/// from the same rulebook rows as the README summary and cli-reference.md.
/// </summary>
public sealed class HelpCommand
{
    public int Run(CliInvocation invocation)
    {
        var topic = invocation.HelpTopic
            ?? invocation.RawTranspilerArg
            ?? invocation.RemainingArguments.FirstOrDefault();

        Console.Write(invocation.UsageHeader);
        Console.WriteLine(
            "\n\nSyntax: effortless [account/]transpiler [Options]\n");

        if (string.IsNullOrWhiteSpace(topic))
        {
            PrintPrimary();
            return 0;
        }

        if (string.Equals(topic, "all", StringComparison.OrdinalIgnoreCase))
        {
            PrintAll();
            return 0;
        }

        var category = CliOptionMetadata.Categories.FirstOrDefault(
            candidate => Matches(candidate.Id, topic)
                         || Matches(candidate.Label, topic));
        if (category is not null)
        {
            PrintCategory(category);
            return 0;
        }

        var option = FindOption(topic);
        if (option is not null)
        {
            PrintOption(option);
            return 0;
        }

        Console.WriteLine($"Unknown help topic '{topic}'.");
        Console.WriteLine();
        PrintFooter();
        return 0;
    }

    private static void PrintPrimary()
    {
        foreach (var category in CliOptionMetadata.Categories)
        {
            var options = CliOptionMetadata.Options
                .Where(option =>
                    option.Category == category.Id
                    && option.Tier == "primary")
                .ToList();
            if (options.Count == 0)
            {
                continue;
            }

            Console.WriteLine(category.Label);
            foreach (var option in options)
            {
                PrintSummaryLine(option);
            }

            Console.WriteLine();
        }

        PrintFooter();
    }

    private static void PrintCategory(CliCategoryInfo category)
    {
        Console.WriteLine(category.Label);
        Console.WriteLine(category.Description);
        Console.WriteLine();
        foreach (var option in CliOptionMetadata.Options
                     .Where(option => option.Category == category.Id))
        {
            PrintSummaryLine(option);
        }

        Console.WriteLine();
        PrintFooter();
    }

    private static void PrintAll()
    {
        foreach (var option in CliOptionMetadata.Options)
        {
            PrintSummaryLine(option);
        }
    }

    private static void PrintOption(CliOptionInfo option)
    {
        Console.WriteLine(option.Flag);
        Console.WriteLine();
        Console.WriteLine(
            string.IsNullOrWhiteSpace(option.HelpDetail)
                ? option.HelpSummary
                : option.HelpDetail);
        Console.WriteLine();
        Console.WriteLine($"  Category: {option.Category}");
        Console.WriteLine($"  Tier:     {option.Tier}");
        if (!string.IsNullOrWhiteSpace(option.ParentOption))
        {
            Console.WriteLine($"  Modifies: {option.ParentOption}");
        }

        if (!string.IsNullOrWhiteSpace(option.VerbFamily))
        {
            Console.WriteLine(
                $"  Family:   {option.VerbFamily} ({option.Scope})");
        }

        Console.WriteLine(
            $"  Aliases:  {Display(option.Aliases)}");
        Console.WriteLine(
            $"  Barewords: {Display(option.BarewordForms)}");
        if (!string.IsNullOrWhiteSpace(option.Example))
        {
            Console.WriteLine();
            Console.WriteLine($"  Example: {option.Example}");
        }
    }

    private static void PrintSummaryLine(CliOptionInfo option) =>
        Console.WriteLine(
            $"  {option.Flag,-26} {option.HelpSummary}");

    private static void PrintFooter()
    {
        Console.WriteLine(
            "Run 'effortless -help <category|option>' for one topic,");
        Console.WriteLine(
            "or 'effortless -help all' for every option.");
    }

    private static CliOptionInfo FindOption(string topic)
    {
        foreach (var option in CliOptionMetadata.Options)
        {
            if (Matches(option.Id, topic)
                || Matches(option.Flag.TrimStart('-'), topic))
            {
                return option;
            }

            if (Split(option.Aliases).Any(alias => Matches(alias, topic))
                || Split(option.BarewordForms)
                    .Any(bareword => Matches(bareword, topic)))
            {
                return option;
            }
        }

        return null;
    }

    private static bool Matches(string candidate, string topic) =>
        string.Equals(
            candidate,
            topic.TrimStart('-'),
            StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> Split(string value) =>
        (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0);

    private static string Display(string value) =>
        string.IsNullOrWhiteSpace(value) ? "(none)" : value;
}
