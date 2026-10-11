using MySocialPlus.Core.Model;
using MySocialPlus.Core.Sample;
using MySocialPlus.Core.Sources;
using MySocialPlus.Core.Store;
using MySocialPlus.Core.Sync;

namespace MySocialPlus.Tests;

public class SyncTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A network that hands out whatever the test puts in it.</summary>
    private sealed class FakeSource : ISource
    {
        public Network Network => Network.Bluesky;
        public List<Item> Next { get; } = [];
        public SourceException? Fail { get; set; }
        public int Fetches { get; private set; }

        public Task PrepareAsync(Account account, CancellationToken ct)
        {
            account.Id ??= "did:" + account.Handle;
            account.DisplayName ??= "Resolved";
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Item>> FetchAsync(Account account, DateTimeOffset now, CancellationToken ct)
        {
            Fetches++;
            if (Fail is not null) throw Fail;
            foreach (var i in Next) i.AccountKey = account.Key;
            return Task.FromResult<IReadOnlyList<Item>>(Next.ToList());
        }
    }

    private static (SocialTree Tree, Account Sarah, ItemCache Cache, FakeSource Source, SyncService Sync) Make()
    {
        var t = SocialTree.CreateNew();
        var p = TreeNode.Person("Sarah");
        t.Add(t.MyFriends, p);
        var a = new Account { Network = Network.Bluesky, Handle = "sarah.test" };
        t.Add(p, TreeNode.ForAccount(a));
        t.Add(p, TreeNode.ForAccount(new Account { Network = Network.Facebook, Handle = "sarah.fb" }));
        var cache = new ItemCache(":memory:");
        var source = new FakeSource();
        return (t, a, cache, source, new SyncService(t, cache, [source], new PollSchedule()));
    }

    [Fact]
    public async Task First_read_learns_the_id_and_files_items_as_seen_then_later_items_are_new()
    {
        var (_, sarah, cache, source, sync) = Make();
        source.Next.Add(CacheAndBadgeTests.Post("", "old", 5));

        var r = await sync.RunDueAsync(T0, default);
        Assert.Equal(1, r.AccountsChecked);                         // Facebook friends are not read
        Assert.Equal("did:sarah.test", sarah.Id);
        Assert.Equal("Resolved", sarah.DisplayName);
        Assert.Equal(T0, sarah.LastChecked);
        Assert.Equal("old", Assert.Single(cache.ForAccounts([sarah.Key])).Id);
        Assert.Empty(cache.UnseenByAccount());                       // connecting does not light up old posts

        Assert.Same(SyncReport.Nothing, await sync.RunDueAsync(T0.AddMinutes(5), default));   // not due yet

        source.Next.Add(CacheAndBadgeTests.Post("", "new", 0.1));
        r = await sync.RunDueAsync(T0.AddMinutes(15), default);
        Assert.Equal(1, r.NewItems);
        Assert.Equal(1, cache.UnseenByAccount()[sarah.Key]);
    }

    [Fact]
    public async Task A_failure_is_recorded_on_the_account_and_backs_off()
    {
        var (_, sarah, _, source, sync) = Make();
        source.Fail = new SourceException(SourceProblem.RateLimited, "Slow down.", TimeSpan.FromHours(1));
        var r = await sync.RunDueAsync(T0, default);
        Assert.Equal("Slow down.", sarah.LastError);
        Assert.Equal("RateLimited", sarah.LastProblem);
        Assert.Single(r.Problems);
        Assert.Null(sarah.LastChecked);
        Assert.False(sync.Schedule.IsDue(sarah.Key, T0.AddMinutes(59)));

        source.Fail = null;
        sync.Schedule.MakeAllDue();
        await sync.RunDueAsync(T0.AddMinutes(1), default);
        Assert.Null(sarah.LastError);
    }

    [Fact]
    public void The_sample_opens_with_badges()
    {
        var dir = Directory.CreateTempSubdirectory("msp-sample").FullName;
        try
        {
            var paths = DataPaths.InFolder(dir);
            SampleData.Write(paths, T0);
            var tree = TreeStore.Load(paths.TreeFile);
            using var cache = new ItemCache(paths.ItemsDb);
            var b = Badges.Compute(tree, cache.UnseenByAccount());

            Assert.Equal(4, b[tree.MyPosts.Id]);                     // a like, a reply, a repost, a follow
            Assert.True(b[tree.MyFriends.Id] > 0);
            var family = tree.MyFriends.Children.First(n => n.Name == "Family");
            Assert.Equal(2, b[family.Id]);                           // Sarah's garden path, Tom's boat
            var jim = tree.AllNodes().First(n => n.Name == "Jim Halloran");
            Assert.Equal(0, b[jim.Id]);                              // Facebook only: no badge, ever
        }
        finally { Directory.Delete(dir, true); }
    }
}
