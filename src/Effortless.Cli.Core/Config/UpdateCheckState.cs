using Newtonsoft.Json;

namespace Effortless.Cli.Config;

/// <summary>
/// ~/.effortless/update_check.json : { Preference: "always"|"never"|null,
/// LastCheckedDate: "yyyy-MM-dd"|null, PendingNotice: string|null }.
///
/// Best-effort like JwtStore, unlike ToolUrls: a missing or corrupt file
/// means "no preference set", never a hard error bubbling to the user. The
/// once-daily background check must never fail a foreground command over a
/// broken preferences file.
/// </summary>
public static class UpdateCheckState
{
    private sealed class State
    {
        public string Preference { get; set; }

        public string LastCheckedDate { get; set; }

        public string PendingNotice { get; set; }
    }

    public static string GetPreference() => TryLoad()?.Preference;

    public static void SetPreference(string preference)
    {
        var state = TryLoad() ?? new State();
        state.Preference = preference;
        TrySave(state);
    }

    public static DateOnly? GetLastCheckedDate()
    {
        var raw = TryLoad()?.LastCheckedDate;
        return DateOnly.TryParse(raw, out var date) ? date : null;
    }

    public static void SetLastCheckedDate(DateOnly date)
    {
        var state = TryLoad() ?? new State();
        state.LastCheckedDate = date.ToString("yyyy-MM-dd");
        TrySave(state);
    }

    /// <summary>
    /// Reads and clears the pending notice atomically: once taken, it will
    /// not be reported again.
    /// </summary>
    public static string TakePendingNotice()
    {
        var state = TryLoad();
        if (state is null || string.IsNullOrEmpty(state.PendingNotice))
        {
            return null;
        }

        var notice = state.PendingNotice;
        state.PendingNotice = null;
        TrySave(state);
        return notice;
    }

    public static void SetPendingNotice(string message)
    {
        var state = TryLoad() ?? new State();
        state.PendingNotice = message;
        TrySave(state);
    }

    private static string GetFilePath() =>
        Path.Combine(UserConfigDir.EffortlessDir.FullName, "update_check.json");

    private static State TryLoad()
    {
        try
        {
            var path = GetFilePath();
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonConvert.DeserializeObject<State>(File.ReadAllText(path));
        }
        catch
        {
            // Best effort: a missing or corrupt file means "no preference set".
            return null;
        }
    }

    private static void TrySave(State state)
    {
        try
        {
            File.WriteAllText(
                GetFilePath(),
                JsonConvert.SerializeObject(state, Formatting.Indented));
        }
        catch
        {
            // Best effort: never let a preferences-file write fail the command.
        }
    }
}
