using MySocialPlus.Core.Model;
using MySocialPlus.Core.Secrets;

namespace MySocialPlus.Core.Sources.Bluesky;

/// <summary>
/// Bluesky as a source. A friend's account: their newest posts (public, no sign-in). One of Ron's
/// own accounts: his newest posts plus the replies, likes, reposts, quotes, mentions and follows
/// from his notifications (needs the app password he saved).
/// </summary>
public sealed class BlueskySource : ISource
{
    public const int FeedSize = 30;
    public const int NotificationCount = 50;

    private readonly BlueskyClient _client;
    private readonly SecretStore _secrets;

    public BlueskySource(BlueskyClient client, SecretStore secrets)
    {
        _client = client;
        _secrets = secrets;
    }

    public Network Network => Network.Bluesky;

    public BlueskyClient Client => _client;

    /// <summary>The secret's name for one of Ron's accounts.</summary>
    public static string SecretName(string handle) => "bluesky-" + BlueskyClient.Clean(handle);

    public async Task PrepareAsync(Account account, CancellationToken ct)
    {
        if (account.Id is not null) return;
        var who = await _client.GetProfileAsync(account.Handle, ct);
        account.Id = who.Did;
        account.Handle = who.Handle;
        account.DisplayName = who.DisplayName;
        account.AvatarUrl = who.AvatarUrl;
    }

    public async Task<IReadOnlyList<Item>> FetchAsync(Account account, DateTimeOffset now, CancellationToken ct)
    {
        var actor = account.Id ?? account.Handle;
        var feed = await _client.GetAuthorFeedAsync(actor, FeedSize, ct);
        var items = BlueskyMapper.FromAuthorFeed(feed, account.Key, now, account.IsMine);
        if (!account.IsMine) return items;

        var password = _secrets.Load(SecretName(account.Handle)) ?? throw new SourceException(SourceProblem.SignInNeeded,
            "Sign in to Bluesky (Accounts ▸ Bluesky sign-in…) so MySocial+ can read the replies and likes on your posts.");
        var notes = BlueskyMapper.FromNotifications(
            await _client.ListNotificationsAsync(account.Handle, password, NotificationCount, ct), account.Key, now);
        items.AddRange(notes);

        // Activity on older posts that are not in the newest 30: fetch those posts so the activity has its post.
        var have = items.Where(i => i.Kind == ItemKind.Post).Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        var missing = notes.Select(n => n.ParentId).OfType<string>().Where(id => !have.Contains(id)).Distinct().ToList();
        for (var i = 0; i < missing.Count; i += 25)
            items.AddRange(BlueskyMapper.FromPosts(await _client.GetPostsAsync(missing.Skip(i).Take(25).ToList(), ct),
                                                   account.Key, now, seen: true));
        return items;
    }
}
