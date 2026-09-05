using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli;

/// <summary>
/// Decides whether a validated remote-tools catalog is recent enough to use.
/// </summary>
public sealed class CatalogFreshnessPolicy
{
    public static readonly TimeSpan MaximumAge = TimeSpan.FromHours(24);
    public static readonly TimeSpan MaximumFutureSkew = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _timeProvider;

    public CatalogFreshnessPolicy(TimeProvider timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public bool IsFresh(JObject root)
    {
        var fetchedAt = TryGetFetchedAt(root);
        if (fetchedAt is null)
        {
            return false;
        }

        var age = UtcNow - fetchedAt.Value;
        return age >= -MaximumFutureSkew && age < MaximumAge;
    }

    /// <summary>
    /// The CLI-stamped instant at which the cached catalog was last validated,
    /// or null when the stamp is missing or unparseable.
    /// </summary>
    public static DateTimeOffset? TryGetFetchedAt(JObject root)
    {
        var token = root?["fetchedAt"];
        if (token?.Type == JTokenType.Date)
        {
            return (token as JValue)?.Value switch
            {
                DateTimeOffset offset => offset,
                DateTime dateTime => new DateTimeOffset(dateTime),
                _ => null,
            };
        }

        return DateTimeOffset.TryParse(
            token?.Value<string>(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal
            | DateTimeStyles.AdjustToUniversal,
            out var fetchedAt)
            ? fetchedAt
            : null;
    }

    /// <summary>
    /// Human-readable age such as "3h 12m" or "2d 5h" (D23 catalog age).
    /// </summary>
    public static string FormatAge(TimeSpan age)
    {
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age.TotalDays >= 1)
        {
            return $"{(int)age.TotalDays}d {age.Hours}h";
        }

        if (age.TotalHours >= 1)
        {
            return $"{(int)age.TotalHours}h {age.Minutes}m";
        }

        if (age.TotalMinutes >= 1)
        {
            return $"{(int)age.TotalMinutes}m";
        }

        return $"{(int)age.TotalSeconds}s";
    }
}
