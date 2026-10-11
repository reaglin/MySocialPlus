using MySocialPlus.Core.Model;

namespace MySocialPlus.Core.Content;

/// <summary>
/// One entry in the content pane: a post with what happened on it (comments, likes, reposts),
/// or a single piece of activity with no post (a follow, a mention, a reply to a post that is
/// not in the cache).
/// </summary>
public sealed class ContentThread
{
    public Item? Post { get; init; }
    /// <summary>Comments and reactions on the post, oldest first — the way a conversation reads.</summary>
    public List<Item> Activity { get; } = [];

    public IEnumerable<Item> All => Post is null ? Activity : Activity.Prepend(Post);
    public DateTimeOffset LatestAt => All.Max(i => i.CreatedAt);
    public bool HasUnseen => All.Any(i => !i.Seen);
    public DateTimeOffset? OldestUnseenAt => All.Where(i => !i.Seen).Select(i => (DateTimeOffset?)i.CreatedAt).Min();
    public int UnseenCount => All.Where(i => !i.Seen).Sum(i => i.Count);
}

/// <summary>Builds the content pane's list from cached items. Time order only — nothing is scored.</summary>
public static class Threads
{
    public static List<ContentThread> Build(IEnumerable<Item> items, SortOrder order)
    {
        // The same post can be cached under two accounts (a friend's reply to Ron is Ron's comment and
        // the friend's post); in one list it shows once.
        var unique = new Dictionary<string, Item>(StringComparer.Ordinal);
        foreach (var i in items)
        {
            var key = $"{i.Kind}|{i.Id}";
            if (unique.TryGetValue(key, out var have)) { if (!i.Seen) have.Seen = false; }
            else unique[key] = i;
        }

        var posts = unique.Values.Where(i => i.Kind == ItemKind.Post)
                                 .GroupBy(i => i.Id).Select(g => g.First())
                                 .ToDictionary(i => i.Id, i => new ContentThread { Post = i }, StringComparer.Ordinal);
        var threads = posts.Values.ToList();
        foreach (var i in unique.Values.Where(i => i.Kind != ItemKind.Post))
        {
            if (i.ParentId is not null && posts.TryGetValue(i.ParentId, out var t)) t.Activity.Add(i);
            else { var lone = new ContentThread(); lone.Activity.Add(i); threads.Add(lone); }
        }
        foreach (var t in threads) t.Activity.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));

        return order == SortOrder.OldestUnreadFirst
            ? threads.Where(t => t.HasUnseen).OrderBy(t => t.OldestUnseenAt)
                     .Concat(threads.Where(t => !t.HasUnseen).OrderByDescending(t => t.LatestAt)).ToList()
            : threads.OrderByDescending(t => t.LatestAt).ToList();
    }
}
