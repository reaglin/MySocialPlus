namespace MySocialPlus.Core.Model;

/// <summary>Saved by name in the cache, so never renumber.</summary>
public enum ItemKind
{
    /// <summary>A post by the account (a friend's post, or one of Ron's own).</summary>
    Post,
    /// <summary>A reply or comment on a post (usually on one of Ron's posts).</summary>
    Comment,
    /// <summary>A like or other reaction. <see cref="Item.Count"/> may be more than one when a network gives only a number.</summary>
    Reaction,
    /// <summary>A repost or share.</summary>
    Repost,
    /// <summary>Someone quoted or mentioned Ron.</summary>
    Mention,
    /// <summary>Someone followed Ron.</summary>
    Follow,
}

/// <summary>
/// One thing that happened on a network, as kept in the item cache. Items belong to an account
/// (<see cref="AccountKey"/>); comments and reactions point at the post they are on
/// (<see cref="ParentId"/>).
/// </summary>
public sealed class Item
{
    public string AccountKey { get; set; } = "";
    public Network Network { get; set; }
    public string Id { get; set; } = "";
    public ItemKind Kind { get; set; }
    public string? ParentId { get; set; }

    /// <summary>Null when the network withholds who did it (Facebook profiles).</summary>
    public string? AuthorName { get; set; }
    public string? AuthorHandle { get; set; }
    public string? AuthorAvatarUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public string Text { get; set; } = "";
    public string? ImageUrl { get; set; }
    /// <summary>The address of the thing on the network, for "Open on Bluesky".</summary>
    public string? Link { get; set; }
    /// <summary>A link card or quoted post, as one line ("Wikipedia — Number Portability…").</summary>
    public string? Attachment { get; set; }
    /// <summary>"Reposted by Bluesky" and the like: why this post is in this account's list.</summary>
    public string? Context { get; set; }

    /// <summary>How many this item stands for (a Facebook "5 new likes" is one item with Count 5).</summary>
    public int Count { get; set; } = 1;
    public int Likes { get; set; }
    public int Replies { get; set; }
    public int Reposts { get; set; }

    public bool Seen { get; set; }

    /// <summary>Who did it, or what the network says when it will not tell.</summary>
    public string AuthorOrHidden =>
        !string.IsNullOrWhiteSpace(AuthorName) ? AuthorName!
        : !string.IsNullOrWhiteSpace(AuthorHandle) ? AuthorHandle!
        : NetworkInfo.For(Network).HiddenAuthorText;
}
