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
        var token = root?["fetchedAt"];
        DateTimeOffset fetchedAt;
        if (token?.Type == JTokenType.Date)
        {
            fetchedAt = (token as JValue)?.Value switch
            {
                DateTimeOffset offset => offset,
                DateTime dateTime => new DateTimeOffset(dateTime),
                _ => default,
            };
        }
        else if (!DateTimeOffset.TryParse(
                     token?.Value<string>(),
                     CultureInfo.InvariantCulture,
                     DateTimeStyles.AssumeUniversal
                     | DateTimeStyles.AdjustToUniversal,
                     out fetchedAt))
        {
            return false;
        }

        var age = UtcNow - fetchedAt;
        return age >= -MaximumFutureSkew && age < MaximumAge;
    }
}
