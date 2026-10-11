using System.Globalization;
using MySocialPlus.Core.Model;

namespace MySocialPlus.Core.Content;

public enum ComingUpKind { Event, Birthday, Reminder }

/// <summary>One line under Coming up: "Sat Oct 18 · Sarah Lee's birthday (turns 42)".</summary>
public sealed record ComingUpItem(DateOnly Date, DateTime? At, ComingUpKind Kind, string Title, string? Detail, TreeNode Node)
{
    public bool IsOverdue(DateOnly today) => Date < today;
}

/// <summary>
/// What is coming up from the tree itself — no network needed: events (following their repeat),
/// birthdays, and any date Ron marked "remind me" (registration renews, warranty ends). Dates that
/// passed in the last week stay listed as overdue, so a missed renewal does not just vanish.
/// </summary>
public static class ComingUp
{
    public static List<ComingUpItem> Build(SocialTree tree, DateOnly today, int days = 30, int overdueDays = 7)
    {
        var until = today.AddDays(days);
        var list = new List<ComingUpItem>();
        foreach (var n in tree.AllNodes())
        {
            if (n.Kind == NodeKind.Event && n.Event is { } e && e.Status is not (EventStatus.Went or EventStatus.Missed)
                && e.NextOn(today) is { } next && DateOnly.FromDateTime(next) <= until)
            {
                var with = e.People.Count == 0 ? null : "with " + JoinNames(e.People.Select(p => p.Name).ToList());
                var where = string.IsNullOrWhiteSpace(e.Location) ? null : e.Location;
                list.Add(new ComingUpItem(DateOnly.FromDateTime(next), e.AllDay ? null : next, ComingUpKind.Event, n.Name,
                                          string.Join(" · ", new[] { where, with }.OfType<string>()) is { Length: > 0 } s ? s : null, n));
            }
            foreach (var d in n.Details)
            {
                if (d.AsDate() is not { } date) continue;
                if (d.Kind == DetailKind.Birthday)
                {
                    var day = Dates.NextAnniversary(date, today);
                    if (day > until) continue;
                    var turns = Dates.HasYear(date) ? $"turns {day.Year - date.Year}" : null;
                    var whose = d.Name.Equals("Birthday", StringComparison.OrdinalIgnoreCase) ? $"{n.Name}'s birthday" : $"{n.Name}: {d.Name}";
                    list.Add(new ComingUpItem(day, null, ComingUpKind.Birthday, whose, turns, n));
                }
                else if (d.Remind && date >= today.AddDays(-overdueDays) && date <= until)
                {
                    list.Add(new ComingUpItem(date, null, ComingUpKind.Reminder, $"{n.Name}: {d.Name}", null, n));
                }
            }
        }
        return list.OrderBy(i => i.Date).ThenBy(i => i.At ?? DateTime.MinValue).ThenBy(i => i.Title, StringComparer.CurrentCulture).ToList();
    }

    /// <summary>"Today", "Tomorrow", "Sat Oct 18", or "3 days ago".</summary>
    public static string When(ComingUpItem i, DateOnly today)
    {
        var day = i.Date == today ? "Today" : i.Date == today.AddDays(1) ? "Tomorrow"
                : i.Date < today ? $"{today.DayNumber - i.Date.DayNumber} days ago"
                : i.Date.ToString("ddd MMM d", CultureInfo.CurrentCulture);
        return i.At is { } t ? $"{day} · {t.ToString("t", CultureInfo.CurrentCulture)}" : day;
    }

    private static string JoinNames(IReadOnlyList<string> names) =>
        names.Count switch
        {
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}",
        };
}
