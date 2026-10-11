using MySocialPlus.Core.Model;

namespace MySocialPlus.Core.Content;

/// <summary>Someone going to an event, with how to reach them (from their details in My Friends).</summary>
public sealed record Contact(string Name, TreeNode? Person, string? Email, string? Phone);

/// <summary>
/// The people going to an event, with their email and phone, so Ron can write to the whole team:
/// an email to everyone, or the lists to paste into a group text.
/// </summary>
public static class Contacts
{
    public static List<Contact> For(SocialTree tree, TreeNode evt)
    {
        var people = evt.Event?.People ?? [];
        return people.Select(p =>
        {
            var node = p.PersonId is null ? null : tree.Find(p.PersonId);
            return new Contact(node?.Name ?? p.Name, node, First(node, DetailKind.Email), First(node, DetailKind.Phone));
        }).ToList();
    }

    /// <summary>The first filled-in detail of that kind — "Email", "Work email", whatever Ron called it.</summary>
    private static string? First(TreeNode? n, DetailKind kind) =>
        n?.Details.FirstOrDefault(d => d.Kind == kind && !string.IsNullOrWhiteSpace(d.Value))?.Value.Trim();

    /// <summary>"sarah@x.com; brenda@y.org" — the separator Outlook and most mail programs accept.</summary>
    public static string EmailList(IEnumerable<Contact> c) => string.Join("; ", c.Select(x => x.Email).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase));

    public static string PhoneList(IEnumerable<Contact> c) => string.Join(", ", c.Select(x => x.Phone).OfType<string>().Distinct());

    /// <summary>A mailto: link to everyone with an address, with the event's name as the subject.</summary>
    public static string? MailTo(IEnumerable<Contact> c, string subject)
    {
        var to = c.Select(x => x.Email).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return to.Count == 0 ? null
            : "mailto:" + string.Join(",", to.Select(Uri.EscapeDataString)) + "?subject=" + Uri.EscapeDataString(subject);
    }

    /// <summary>Who is missing an email or phone, so Ron knows whose details to fill in.</summary>
    public static IReadOnlyList<string> Missing(IEnumerable<Contact> c, DetailKind kind) =>
        c.Where(x => (kind == DetailKind.Email ? x.Email : x.Phone) is null).Select(x => x.Name).ToList();
}
