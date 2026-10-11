namespace MySocialPlus.Core.Model;

/// <summary>A social network or feed kind. Saved by name in tree.json, so never renumber.</summary>
public enum Network
{
    Bluesky,
    Facebook,
    Instagram,
    Mastodon,
    YouTube,
    Rss,
    Reddit,
    X,
    Threads,
    LinkedIn,
    Other,
}

/// <summary>
/// What MySocial+ can and cannot see on a network, in words a person can act on. The window uses
/// this to say why a node has no badge, or why a comment has no name (CLAUDE.md, rule 5).
/// </summary>
public sealed record NetworkInfo(
    Network Network,
    string Name,
    bool CanReadFriends,
    bool CanReadMyPosts,
    string WhyNot,
    string HiddenAuthorText,
    string? ProfileUrlFormat)
{
    public static NetworkInfo For(Network n) => n switch
    {
        Network.Bluesky => new(n, "Bluesky", true, true, "",
            "someone", "https://bsky.app/profile/{0}"),
        Network.Facebook => new(n, "Facebook", false, false,
            "Facebook does not let any program read a friend's posts. MySocial+ can open their profile for you, " +
            "but it cannot tell you when something is new.",
            "name hidden by Facebook", "https://www.facebook.com/{0}"),
        Network.Instagram => new(n, "Instagram", false, false,
            "Instagram does not let any program read a friend's posts, or a personal account's own comments.",
            "name hidden by Instagram", "https://www.instagram.com/{0}/"),
        Network.Mastodon => new(n, "Mastodon", false, false,
            "Reading Mastodon is planned but not built yet.", "someone", null),
        Network.YouTube => new(n, "YouTube", false, false,
            "Reading YouTube is planned but not built yet.", "someone", "https://www.youtube.com/{0}"),
        Network.Rss => new(n, "RSS feed", false, false,
            "Reading RSS feeds is planned but not built yet.", "someone", null),
        Network.Reddit => new(n, "Reddit", false, false,
            "Reading Reddit is planned but not built yet.", "someone", "https://www.reddit.com/user/{0}"),
        Network.X => new(n, "X", false, false,
            "X charges for every program that reads it, so MySocial+ does not.", "someone", "https://x.com/{0}"),
        Network.Threads => new(n, "Threads", false, false,
            "Threads does not let any program read a friend's posts.", "someone", "https://www.threads.net/@{0}"),
        Network.LinkedIn => new(n, "LinkedIn", false, false,
            "LinkedIn does not let any program read a friend's posts.", "someone", "https://www.linkedin.com/in/{0}"),
        _ => new(n, "Other", false, false, "MySocial+ cannot read this network.", "someone", null),
    };

    /// <summary>The profile address for a handle, or the handle itself when it is already an address.</summary>
    public string? ProfileUrl(string handle)
    {
        if (string.IsNullOrWhiteSpace(handle)) return null;
        if (handle.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            handle.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return handle;
        return ProfileUrlFormat is null ? null
            : string.Format(System.Globalization.CultureInfo.InvariantCulture, ProfileUrlFormat,
                            Uri.EscapeDataString(handle.Trim().TrimStart('@')));
    }

    public static IReadOnlyList<Network> All { get; } = Enum.GetValues<Network>();
}
