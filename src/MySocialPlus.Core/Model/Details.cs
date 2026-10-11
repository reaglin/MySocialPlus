using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MySocialPlus.Core.Model;

/// <summary>
/// What kind of value a detail holds — only so the window can offer the right box (a date picker,
/// a box that opens a link) and so dates can be reminded. Saved by name, so never renumber.
/// </summary>
public enum DetailKind { Text, Note, Date, Birthday, Number, Money, Link, Phone, Email }

/// <summary>
/// One name/value pair: "License plate: ABC 123", "Birthday: 1984-03-12", "Likes: jazz, sailing".
/// Values are kept as text so nothing Ron types is ever refused; a date is <c>yyyy-MM-dd</c> (a
/// birthday whose year is not known is <c>--MM-dd</c>).
/// </summary>
public sealed class Detail
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public DetailKind Kind { get; set; }

    /// <summary>A date to show under Coming up (registration renews, warranty ends). Birthdays always are.</summary>
    public bool Remind { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    public bool IsDate => Kind is DetailKind.Date or DetailKind.Birthday;

    /// <summary>The date in the value, if it holds one. A birthday without a year comes back in year 4 (a leap year).</summary>
    public DateOnly? AsDate() => Dates.Parse(Value);
}

/// <summary>A dated entry in a node's log: "2026-09-02 · Oil change, tire rotation · $89.95".</summary>
public sealed class LogEntry
{
    public DateOnly Date { get; set; }
    public string Text { get; set; } = "";
    /// <summary>What it cost, as typed ("89.95"); empty when it does not apply.</summary>
    public string? Cost { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public enum Repeat { None, Weekly, Monthly, Yearly }

/// <summary>Where Ron stands with an event: planning it, going, went, or missed it.</summary>
public enum EventStatus { Planned, Going, Went, Missed }

/// <summary>When and where an event is, how often, and who Ron is going with.</summary>
public sealed class EventInfo
{
    /// <summary>Local date and time; the time is ignored when <see cref="AllDay"/>.</summary>
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public bool AllDay { get; set; }
    public string? Location { get; set; }
    public Repeat Repeat { get; set; }
    public EventStatus Status { get; set; }
    public List<EventPerson> People { get; set; } = [];

    /// <summary>The plan: timed steps ("Fri 5 PM · Packet pickup"), kept in time order.</summary>
    public List<AgendaItem> Agenda { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    /// <summary>The first time it happens on or after <paramref name="from"/>, following the repeat; null if never.</summary>
    public DateTime? NextOn(DateOnly from)
    {
        if (Start is not { } s) return null;
        var t = s;
        var start = from.ToDateTime(TimeOnly.MinValue);
        if (t >= start || Repeat == Repeat.None) return t >= start ? t : null;
        for (var i = 0; i < 5000 && t < start; i++)
            t = Repeat switch { Repeat.Weekly => t.AddDays(7), Repeat.Monthly => s.AddMonths(i + 1), _ => s.AddYears(i + 1) };
        return t;
    }
}

/// <summary>One step of an event's plan. <see cref="At"/> may be empty for "sometime" items.</summary>
public sealed class AgendaItem
{
    public DateTime? At { get; set; }
    public string Text { get; set; } = "";
    public string? Where { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Someone going: a person in the tree (by id), or just a name for someone who is not.</summary>
public sealed class EventPerson
{
    public string? PersonId { get; set; }
    public string Name { get; set; } = "";
    /// <summary>For someone not in My Friends: how to reach them. A friend's come from their own details.</summary>
    public string? Email { get; set; }
    public string? Phone { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public static class Dates
{
    /// <summary>"2026-10-18", "--03-12" (a birthday with no year), or a date as the person's culture writes it.</summary>
    public static DateOnly? Parse(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (s.StartsWith("--", StringComparison.Ordinal) &&
            DateOnly.TryParseExact("0004-" + s[2..], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var noYear))
            return noYear;
        if (DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso)) return iso;
        return DateOnly.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.None, out var local) ? local : null;
    }

    public static string Format(DateOnly d, bool knownYear = true) =>
        knownYear ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "--" + d.ToString("MM-dd", CultureInfo.InvariantCulture);

    public static bool HasYear(DateOnly d) => d.Year != 4;

    /// <summary>The next time this month and day comes round, on or after <paramref name="from"/> (29 Feb → 28 Feb in other years).</summary>
    public static DateOnly NextAnniversary(DateOnly d, DateOnly from)
    {
        DateOnly In(int year) => new(year, d.Month, Math.Min(d.Day, DateTime.DaysInMonth(year, d.Month)));
        var next = In(from.Year);
        return next >= from ? next : In(from.Year + 1);
    }
}
