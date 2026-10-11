using System.Text.Json.Nodes;
using MySocialPlus.Core.Model;

namespace MySocialPlus.Core.Sources.Bluesky;

/// <summary>Someone on Bluesky, as a follow list or a profile gives them.</summary>
public sealed record BlueskyActor(string Did, string Handle, string? DisplayName, string? AvatarUrl, string? Description)
{
    public string Name => string.IsNullOrWhiteSpace(DisplayName) ? Handle : DisplayName!;
}

/// <summary>
/// Turns Bluesky's answers (AT Protocol lexicons <c>app.bsky.*</c>) into cache items. Pure and
/// defensive: a field that is missing or the wrong shape is skipped, never a crash. Tested against
/// answers saved from the real API (tests/fixtures).
/// </summary>
public static class BlueskyMapper
{
    /// <summary>
    /// <c>app.bsky.feed.getAuthorFeed</c> → the account's posts (and reposts, with who reposted).
    /// Ron's own posts are marked seen: he wrote them, so they are not news to him.
    /// </summary>
    public static List<Item> FromAuthorFeed(JsonNode? root, string accountKey, DateTimeOffset fetchedAt, bool mine)
    {
        var items = new List<Item>();
        if (root?["feed"] is not JsonArray feed) return items;
        foreach (var entry in feed)
        {
            var item = FromPostView(entry?["post"], accountKey, fetchedAt);
            if (item is null) continue;
            var reason = entry!["reason"];
            if (Str(reason, "$type") == "app.bsky.feed.defs#reasonRepost")
            {
                var by = Actor(reason!["by"]);
                item.Id = Str(reason, "uri") ?? item.Id + "#repost";
                if (Date(reason, "indexedAt") is { } at) item.CreatedAt = at;
                item.Context = mine ? "You reposted this" : $"Reposted by {by?.Name ?? "them"}";
            }
            else if (entry["reply"]?["parent"] is JsonNode parent && Actor(parent["author"]) is { } to)
            {
                item.Context = $"Replying to {to.Name}";
            }
            item.Seen = mine;
            items.Add(item);
        }
        return items;
    }

    /// <summary><c>app.bsky.feed.getPosts</c> → posts (the ones Ron's notifications are about).</summary>
    public static List<Item> FromPosts(JsonNode? root, string accountKey, DateTimeOffset fetchedAt, bool seen)
    {
        var items = new List<Item>();
        if (root?["posts"] is not JsonArray posts) return items;
        foreach (var p in posts)
            if (FromPostView(p, accountKey, fetchedAt) is { } item) { item.Seen = seen; items.Add(item); }
        return items;
    }

    /// <summary>A <c>postView</c>: text, author, counts, the first picture, and a line for a link card or quote.</summary>
    public static Item? FromPostView(JsonNode? post, string accountKey, DateTimeOffset fetchedAt)
    {
        var uri = Str(post, "uri");
        if (post is null || uri is null) return null;
        var author = Actor(post["author"]);
        var record = post["record"];
        var item = new Item
        {
            AccountKey = accountKey,
            Network = Network.Bluesky,
            Id = uri,
            Kind = ItemKind.Post,
            AuthorName = author?.DisplayName is { Length: > 0 } dn ? dn : author?.Handle,
            AuthorHandle = author?.Handle,
            AuthorAvatarUrl = author?.AvatarUrl,
            CreatedAt = Date(record, "createdAt") ?? Date(post, "indexedAt") ?? fetchedAt,
            FetchedAt = fetchedAt,
            Text = Str(record, "text") ?? "",
            Link = WebLink(uri, author?.Handle),
            Likes = Int(post, "likeCount"),
            Replies = Int(post, "replyCount"),
            Reposts = Int(post, "repostCount") + Int(post, "quoteCount"),
        };
        (item.ImageUrl, item.Attachment) = Embed(post["embed"]);
        return item;
    }

    private static (string? Image, string? Attachment) Embed(JsonNode? embed)
    {
        switch (Str(embed, "$type"))
        {
            case "app.bsky.embed.images#view":
                return (embed!["images"] is JsonArray imgs && imgs.Count > 0 ? Str(imgs[0], "thumb") : null, null);
            case "app.bsky.embed.video#view":
                return (Str(embed, "thumbnail"), "Video — open on Bluesky to play it");
            case "app.bsky.embed.external#view":
                var ext = embed!["external"];
                var title = Str(ext, "title");
                var link = Str(ext, "uri");
                return (Str(ext, "thumb"), string.IsNullOrWhiteSpace(title) ? link : $"{title} — {link}");
            case "app.bsky.embed.record#view":
                return (null, Quote(embed!["record"]));
            case "app.bsky.embed.recordWithMedia#view":
                var (img, att) = Embed(embed!["media"]);
                var quote = Quote(embed["record"]?["record"]);
                return (img, quote is null ? att : att is null ? quote : $"{att}\n{quote}");
            default:
                return (null, null);
        }
    }

    /// <summary>A quoted post as one line: "Quoting Sarah Lee: Finally finished…".</summary>
    private static string? Quote(JsonNode? rec)
    {
        if (rec is null) return null;
        var text = Str(rec["value"], "text");
        if (text is null) return Str(rec, "$type") == "app.bsky.embed.record#viewNotFound" ? "Quoting a post that was deleted" : null;
        var who = Actor(rec["author"])?.Name ?? "a post";
        return $"Quoting {who}: {Shorten(text, 200)}";
    }

    /// <summary>
    /// <c>app.bsky.notification.listNotifications</c> → activity on Ron's posts: replies are comments,
    /// likes are reactions, reposts and quotes point at the post they are about, and follows and
    /// mentions stand alone. Reasons this program does not show are skipped. Bluesky's own "read" flag
    /// is ignored — "new" means new in MySocial+ (rule 3).
    /// </summary>
    public static List<Item> FromNotifications(JsonNode? root, string accountKey, DateTimeOffset fetchedAt)
    {
        var items = new List<Item>();
        if (root?["notifications"] is not JsonArray list) return items;
        foreach (var n in list)
        {
            var uri = Str(n, "uri");
            if (n is null || uri is null) continue;
            var author = Actor(n["author"]);
            var record = n["record"];
            var subject = Str(n, "reasonSubject");
            (ItemKind Kind, string? Parent, string Text)? what = Str(n, "reason") switch
            {
                "reply" => (ItemKind.Comment, subject ?? Str(record?["reply"]?["parent"], "uri"), Str(record, "text") ?? ""),
                "like" or "like-via-repost" => (ItemKind.Reaction, subject, "liked this"),
                "repost" or "repost-via-repost" => (ItemKind.Repost, subject, "reposted this"),
                "quote" => (ItemKind.Mention, subject, Str(record, "text") ?? "quoted this"),
                "mention" => (ItemKind.Mention, null, Str(record, "text") ?? "mentioned you"),
                "follow" => (ItemKind.Follow, null, "followed you"),
                _ => null,
            };
            if (what is not (var kind, var parent, var text)) continue;

            items.Add(new Item
            {
                AccountKey = accountKey,
                Network = Network.Bluesky,
                Id = uri,
                Kind = kind,
                ParentId = parent,
                AuthorName = author?.DisplayName is { Length: > 0 } dn ? dn : author?.Handle,
                AuthorHandle = author?.Handle,
                AuthorAvatarUrl = author?.AvatarUrl,
                CreatedAt = Date(n, "indexedAt") ?? Date(record, "createdAt") ?? fetchedAt,
                FetchedAt = fetchedAt,
                Text = text,
                Link = kind is ItemKind.Comment or ItemKind.Mention ? WebLink(uri, author?.Handle)
                     : author?.Handle is { } h ? $"https://bsky.app/profile/{h}" : null,
            });
        }
        return items;
    }

    /// <summary><c>app.bsky.graph.getFollows</c> → the people, and the cursor for the next page (null at the end).</summary>
    public static (List<BlueskyActor> People, string? Cursor) FromFollows(JsonNode? root)
    {
        var people = new List<BlueskyActor>();
        if (root?["follows"] is JsonArray list)
            foreach (var a in list)
                if (Actor(a) is { } actor) people.Add(actor);
        return (people, Str(root, "cursor"));
    }

    public static BlueskyActor? Actor(JsonNode? a)
    {
        var did = Str(a, "did");
        var handle = Str(a, "handle");
        return did is null || handle is null ? null
            : new BlueskyActor(did, handle, Str(a, "displayName"), Str(a, "avatar"), Str(a, "description"));
    }

    /// <summary><c>at://did:plc:x/app.bsky.feed.post/abc</c> → <c>https://bsky.app/profile/handle/post/abc</c>.</summary>
    public static string? WebLink(string atUri, string? handle)
    {
        if (!atUri.StartsWith("at://", StringComparison.Ordinal)) return null;
        var parts = atUri[5..].Split('/');
        if (parts.Length != 3 || parts[1] != "app.bsky.feed.post") return null;
        return $"https://bsky.app/profile/{handle ?? parts[0]}/post/{parts[2]}";
    }

    private static string Shorten(string s, int max) => s.Length <= max ? s : s[..max].TrimEnd() + "…";

    private static string? Str(JsonNode? n, string name)
    {
        try { return n is JsonObject o && o[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null; }
        catch (InvalidOperationException) { return null; }
    }

    private static int Int(JsonNode? n, string name)
    {
        try { return n is JsonObject o && o[name] is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0; }
        catch (InvalidOperationException) { return 0; }
    }

    private static DateTimeOffset? Date(JsonNode? n, string name) =>
        Str(n, name) is { } s && DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                                                         System.Globalization.DateTimeStyles.AssumeUniversal, out var d) ? d : null;
}
