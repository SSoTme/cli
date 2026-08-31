using System.Text;

namespace Effortless.Cli.Text;

public static class NameHelpers
{
    public static string SafeToString(this object value)
    {
        if (ReferenceEquals(value, null))
        {
            return string.Empty;
        }

        return value.ToString();
    }

    public static string ToCamelString(this string titleString)
    {
        titleString = titleString.SafeToString().Replace("-", " ");
        var sb = new StringBuilder();
        var prevChar = ' ';
        foreach (char currentChar in titleString)
        {
            if (!char.IsLetterOrDigit(currentChar))
            {
                // Do nothing in this case
            }
            else if (prevChar == ' ')
            {
                sb.Append(currentChar.SafeToString().ToUpper());
            }
            else
            {
                sb.Append(currentChar);
            }

            prevChar = currentChar;
        }

        return sb.ToString().Replace(" ", "");
    }

    public static string TitleFromCamel(this string camelString)
    {
        camelString = string.Join(
            "",
            camelString.SafeToString().ToList()
                .Select(feChar => (char.IsUpper(feChar) ? " " : "") + feChar.SafeToString()));
        return camelString.Trim();
    }

    public static string ToTitle(this string name)
    {
        return name.ToCamelString().TitleFromCamel();
    }

    public static string ToName(this string nameCandidate)
    {
        string name = nameCandidate.SafeToString();
        name = name.ToCamelString();
        return name;
    }

    public static string ToTitleCase(this string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        text = text.SafeToString();

        if (char.IsLower(text[0]))
        {
            text = text.First().ToString().ToUpper() + text.Substring(1);
        }

        return string.Join(
                string.Empty,
                text.SafeToString()
                    .Select(c => char.IsUpper(c) ? " " + c : c.ToString())
                    .ToArray())
            .Trim();
    }

    public static string SanitizeUrlForFilename(this string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return string.Empty;
        }

        return url
            .Replace("/", "")
            .Replace(".", "")
            .Replace(":", "")
            .Replace("?", "")
            .Replace("&", "")
            .Replace("=", "")
            .Replace(" ", "-")
            .ToLower();
    }

    public static string StripParamNumber(this string fullParamString)
    {
        fullParamString = fullParamString.SafeToString();
        if (fullParamString.StartsWith("param") &&
            fullParamString.Substring(0, "paramNN=".Length).Contains("="))
        {
            fullParamString = fullParamString.Substring(fullParamString.IndexOf("=") + 1);
        }

        return fullParamString;
    }

    public static string LowerHyphenName(string name)
    {
        return name.ToTitle().Replace(" ", "-").ToLower();
    }
}
