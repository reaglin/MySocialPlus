using MySocialPlus.Core.Content;
using MySocialPlus.Core.Model;

namespace MySocialPlus.App.ViewModels;

/// <summary>One post or piece of activity as the content pane shows it.</summary>
public sealed class ItemVm
{
    public ItemVm(Item item, DateTimeOffset now)
    {
        Item = item;
        TimeText = When.Ago(item.CreatedAt, now);
        FullTime = When.Full(item.CreatedAt);
    }

    public Item Item { get; }
    public string Author => Item.AuthorOrHidden;
    /// <summary>"@handle · Bluesky", or just the network when the name is hidden.</summary>
    public string Source => string.IsNullOrWhiteSpace(Item.AuthorName) || string.IsNullOrWhiteSpace(Item.AuthorHandle)
        ? NetworkInfo.For(Item.Network).Name
        : $"@{Item.AuthorHandle} · {NetworkInfo.For(Item.Network).Name}";
    public bool NameHidden => string.IsNullOrWhiteSpace(Item.AuthorName) && string.IsNullOrWhiteSpace(Item.AuthorHandle);
    public string? Avatar => Item.AuthorAvatarUrl;
    public string Initial => Author.Length > 0 ? Author[..1].ToUpperInvariant() : "?";
    public string TimeText { get; }
    public string FullTime { get; }
    public string Text => Item.Text;
    public bool HasText => !string.IsNullOrWhiteSpace(Item.Text);
    public string? Image => Item.ImageUrl;
    public string? Attachment => Item.Attachment;
    public string? Context => Item.Context;
    public string? Link => Item.Link;
    public bool IsNew => !Item.Seen;
    public string OpenText => $"Open on {NetworkInfo.For(Item.Network).Name}";
    public string ReplyText => $"Reply on {NetworkInfo.For(Item.Network).Name}";
    public string ReplyTip => $"Opens this on {NetworkInfo.For(Item.Network).Name} in your web browser. Write your answer there — MySocial+ never posts for you.";

    public string? Counts
    {
        get
        {
            var parts = new List<string>();
            if (Item.Likes > 0) parts.Add($"♥ {Item.Likes}");
            if (Item.Replies > 0) parts.Add($"💬 {Item.Replies}");
            if (Item.Reposts > 0) parts.Add($"⟲ {Item.Reposts}");
            return parts.Count == 0 ? null : string.Join("   ", parts);
        }
    }

    public bool IsComment => Item.Kind is ItemKind.Comment or ItemKind.Mention;

    /// <summary>A like, repost or follow: one short line.</summary>
    public string ActivityLine => Item.Kind switch
    {
        ItemKind.Reaction when Item.Count > 1 => $"{Item.Count} new reactions",
        ItemKind.Reaction => $"{Author} liked this",
        ItemKind.Repost => $"{Author} reposted this",
        ItemKind.Follow => $"{Author} followed you",
        ItemKind.Mention when Item.ParentId is not null => $"{Author} quoted this",
        ItemKind.Mention => $"{Author} mentioned you",
        ItemKind.Comment => $"{Author} replied",
        _ => Author,
    };

    public string Glyph => Item.Kind switch
    {
        ItemKind.Reaction => "",   // heart
        ItemKind.Repost => "",     // repeat
        ItemKind.Follow => "",     // add friend
        ItemKind.Mention => "",    // message
        _ => "",                   // chat bubbles
    };

    public string SpokenText => (IsNew ? "New. " : "") + (Item.Kind == ItemKind.Post ? $"{Author}, {TimeText}: {Text}" : $"{ActivityLine}, {TimeText}. {Text}");
}

/// <summary>A post with what happened on it, or a lone piece of activity.</summary>
public sealed class ThreadVm
{
    public ThreadVm(ContentThread t, DateTimeOffset now)
    {
        Post = t.Post is null ? null : new ItemVm(t.Post, now);
        Activity = t.Activity.Select(i => new ItemVm(i, now)).ToList();
        IsNew = t.HasUnseen;
        NewCount = t.UnseenCount;
    }

    public ItemVm? Post { get; }
    public bool HasPost => Post is not null;
    public List<ItemVm> Activity { get; }
    public bool HasActivity => Activity.Count > 0;
    public bool IsNew { get; }
    public int NewCount { get; }

    /// <summary>"3 new" on a post whose activity is new; "New" on a new post.</summary>
    public string NewText => Post is { IsNew: true } && NewCount == 1 ? "New" : $"{NewCount} new";

    public string SpokenText => (IsNew ? $"{NewText}. " : "") + (Post?.SpokenText ?? Activity.FirstOrDefault()?.SpokenText ?? "");
}
