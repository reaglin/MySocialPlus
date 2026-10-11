using System.Globalization;
using System.Text;
using MySocialPlus.Core.Model;

namespace MySocialPlus.Core.Content;

/// <summary>
/// An event as an iCalendar file (RFC 5545), so "Add to my calendar" opens it in Outlook or any
/// calendar program. Times are written as local "floating" times — the event happens at 7:30 PM
/// wherever Ron is — which every calendar program accepts.
/// </summary>
public static class Ics
{
    public static string For(TreeNode node, DateTime stamp)
    {
        var e = node.Event ?? throw new InvalidOperationException("Only an event can go in a calendar.");
        if (e.Start is not { } start) throw new InvalidOperationException("Give the event a date first.");

        var sb = new StringBuilder();
        void Line(string s) => sb.Append(Fold(s)).Append("\r\n");
        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//Ron Eaglin//MySocial+//EN");
        Line("BEGIN:VEVENT");
        Line($"UID:{node.Id}@mysocialplus");
        Line("DTSTAMP:" + stamp.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
        if (e.AllDay)
        {
            Line("DTSTART;VALUE=DATE:" + start.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            Line("DTEND;VALUE=DATE:" + (e.End ?? start).Date.AddDays(1).ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        }
        else
        {
            Line("DTSTART:" + Local(start));
            Line("DTEND:" + Local(e.End is { } end && end > start ? end : start.AddHours(1)));
        }
        var rule = e.Repeat switch { Repeat.Weekly => "WEEKLY", Repeat.Monthly => "MONTHLY", Repeat.Yearly => "YEARLY", _ => null };
        if (rule is not null) Line("RRULE:FREQ=" + rule);
        Line("SUMMARY:" + Escape(node.Name));
        if (!string.IsNullOrWhiteSpace(e.Location)) Line("LOCATION:" + Escape(e.Location));

        var notes = new List<string>();
        if (e.People.Count > 0) notes.Add("With: " + string.Join(", ", e.People.Select(p => p.Name)));
        foreach (var a in e.Agenda.OrderBy(a => a.At ?? DateTime.MaxValue))
            notes.Add((a.At is { } at ? at.ToString("ddd h:mm tt", CultureInfo.InvariantCulture) + " — " : "") + a.Text +
                      (string.IsNullOrWhiteSpace(a.Where) ? "" : $" ({a.Where})"));
        notes.AddRange(node.Details.Where(d => d.Value.Length > 0).Select(d => $"{d.Name}: {d.Value}"));
        if (notes.Count > 0) Line("DESCRIPTION:" + Escape(string.Join("\n", notes)));
        Line("END:VEVENT");
        Line("END:VCALENDAR");
        return sb.ToString();
    }

    private static string Local(DateTime t) => t.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(";", "\\;", StringComparison.Ordinal)
         .Replace(",", "\\,", StringComparison.Ordinal).Replace("\r\n", "\\n", StringComparison.Ordinal)
         .Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>Lines longer than 75 octets are folded: a line break and a space (RFC 5545 3.1).</summary>
    private static string Fold(string line)
    {
        var bytes = Encoding.UTF8.GetByteCount(line);
        if (bytes <= 75) return line;
        var sb = new StringBuilder();
        var count = 0;
        foreach (var r in line.EnumerateRunes())
        {
            var n = r.Utf8SequenceLength;
            if (count + n > 75) { sb.Append("\r\n "); count = 1; }
            sb.Append(r.ToString());
            count += n;
        }
        return sb.ToString();
    }
}
