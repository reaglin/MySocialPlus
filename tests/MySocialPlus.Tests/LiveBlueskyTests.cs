using MySocialPlus.Core.Model;
using MySocialPlus.Core.Secrets;
using MySocialPlus.Core.Sources.Bluesky;
using MySocialPlus.Core.Store;
using MySocialPlus.Core.Sync;

namespace MySocialPlus.Tests;

/// <summary>Runs only when MSP_LIVE=1: the tests otherwise never touch the network (CLAUDE.md).</summary>
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MSP_LIVE") != "1")
            Skip = "Talks to the real Bluesky. Set MSP_LIVE=1 to run it.";
    }
}

public class LiveBlueskyTests
{
    /// <summary>A friend's account read end to end through the real public API: resolve, read, cache, badge.</summary>
    [LiveFact]
    public async Task A_real_public_account_is_read_and_filed()
    {
        var dir = Directory.CreateTempSubdirectory("msp-live").FullName;
        try
        {
            using var http = new HttpClient();
            var source = new BlueskySource(new BlueskyClient(http), new SecretStore(dir));
            var tree = SocialTree.CreateNew();
            var person = TreeNode.Person("Bluesky");
            tree.Add(tree.MyFriends, person);
            var account = new Account { Network = Network.Bluesky, Handle = "bsky.app" };
            tree.Add(person, TreeNode.ForAccount(account));
            using var cache = new ItemCache(Path.Combine(dir, "items.db"));
            var sync = new SyncService(tree, cache, [source], new PollSchedule());

            var report = await sync.RunDueAsync(DateTimeOffset.Now, default);

            Assert.Empty(report.Problems);
            Assert.StartsWith("did:plc:", account.Id, StringComparison.Ordinal);
            var items = cache.ForAccounts([account.Key]);
            Assert.NotEmpty(items);
            Assert.All(items, i => Assert.True(i.Seen));   // the first read is the baseline
            Assert.All(items, i => Assert.StartsWith("https://bsky.app/profile/", i.Link, StringComparison.Ordinal));

            var follows = await source.Client.GetAllFollowsAsync("pfrazee.com", default, max: 150);
            Assert.True(follows.Count > 100);                // pfrazee.com follows ~700: more than one page
        }
        finally { Directory.Delete(dir, true); }
    }
}
