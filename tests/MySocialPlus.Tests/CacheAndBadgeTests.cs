using MySocialPlus.Core.Content;
using MySocialPlus.Core.Model;
using MySocialPlus.Core.Store;
using MySocialPlus.Core.Sync;

namespace MySocialPlus.Tests;

public class CacheAndBadgeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    internal static Item Post(string key, string id, double hoursAgo, bool seen = false) => new()
    {
        AccountKey = key, Network = Network.Bluesky, Id = id, Kind = ItemKind.Post, AuthorName = "A",
        CreatedAt = Now.AddHours(-hoursAgo), FetchedAt = Now, Text = id, Seen = seen,
    };

    [Fact]
    public void Upsert_adds_new_refreshes_old_and_keeps_seen()
    {
        using var cache = new ItemCache(":memory:");
        Assert.Equal(2, cache.Upsert([Post("k", "1", 2), Post("k", "2", 1)]));
        cache.MarkSeen(["k"]);

        var changed = Post("k", "1", 2);
        changed.Text = "edited";
        changed.Likes = 9;
        Assert.Equal(1, cache.Upsert([changed, Post("k", "3", 0.5)]));

        var all = cache.ForAccounts(["k"]);
        Assert.Equal(["3", "2", "1"], all.Select(i => i.Id));       // newest first
        var one = all.Single(i => i.Id == "1");
        Assert.Equal("edited", one.Text);
        Assert.Equal(9, one.Likes);
        Assert.True(one.Seen);                                       // refreshing does not un-see
        Assert.False(all.Single(i => i.Id == "3").Seen);
    }

    [Fact]
    public void First_read_arrives_as_seen()
    {
        using var cache = new ItemCache(":memory:");
        cache.Upsert([Post("k", "1", 1)], asSeen: true);
        Assert.Empty(cache.UnseenByAccount());
    }

    [Fact]
    public void A_reaction_count_that_grows_is_new_again()
    {
        using var cache = new ItemCache(":memory:");
        var likes = new Item { AccountKey = "fb", Network = Network.Facebook, Id = "post1/likes", Kind = ItemKind.Reaction, Count = 3, CreatedAt = Now, FetchedAt = Now };
        cache.Upsert([likes]);
        cache.MarkSeen(["fb"]);
        likes.Count = 5;
        cache.Upsert([likes]);
        Assert.Equal(5, cache.UnseenByAccount()["fb"]);
    }

    [Fact]
    public void Same_id_under_two_accounts_is_two_rows()
    {
        using var cache = new ItemCache(":memory:");
        Assert.Equal(2, cache.Upsert([Post("ron", "x", 1), Post("sarah", "x", 1)]));
        cache.Forget("sarah");
        Assert.Equal(1, cache.Count());
    }

    /// <summary>Plan 1.4's test, word for word.</summary>
    [Fact]
    public void Three_new_items_under_a_person_show_3_on_the_person_topic_and_root_and_opening_the_person_clears_them()
    {
        var t = SocialTree.CreateNew();
        var family = TreeNode.Topic("Family");
        t.Add(t.MyFriends, family);
        var sarah = TreeNode.Person("Sarah");
        t.Add(family, sarah);
        t.Add(sarah, TreeNode.ForAccount(new Account { Network = Network.Bluesky, Handle = "sarah.test" }));
        var tom = TreeNode.Person("Tom");
        t.Add(family, tom);
        t.Add(tom, TreeNode.ForAccount(new Account { Network = Network.Bluesky, Handle = "tom.test" }));
        var key = sarah.Children[0].Account!.Key;

        using var cache = new ItemCache(":memory:");
        cache.Upsert([Post(key, "1", 3), Post(key, "2", 2), Post(key, "3", 1), Post("bluesky:tom.test", "4", 1, seen: true)]);

        var b = Badges.Compute(t, cache.UnseenByAccount());
        Assert.Equal(3, b[sarah.Id]);
        Assert.Equal(3, b[family.Id]);
        Assert.Equal(3, b[t.MyFriends.Id]);
        Assert.Equal(0, b[tom.Id]);
        Assert.Equal(0, b[t.MyPosts.Id]);

        cache.MarkSeen(sarah.AccountKeys());   // what opening Sarah does
        b = Badges.Compute(t, cache.UnseenByAccount());
        Assert.Equal(0, b[sarah.Id]);
        Assert.Equal(0, b[family.Id]);
        Assert.Equal(0, b[t.MyFriends.Id]);
    }

    [Fact]
    public void Threads_put_activity_under_its_post_and_order_by_time()
    {
        var p1 = Post("ron", "p1", 10, seen: true);
        var p2 = Post("ron", "p2", 5, seen: true);
        var reply = new Item { AccountKey = "ron", Id = "r1", Kind = ItemKind.Comment, ParentId = "p1", CreatedAt = Now.AddHours(-1), Text = "hi" };
        var like = new Item { AccountKey = "ron", Id = "l1", Kind = ItemKind.Reaction, ParentId = "p1", CreatedAt = Now.AddHours(-2) };
        var follow = new Item { AccountKey = "ron", Id = "f1", Kind = ItemKind.Follow, CreatedAt = Now.AddHours(-3), Seen = true };
        var orphan = new Item { AccountKey = "ron", Id = "r2", Kind = ItemKind.Comment, ParentId = "gone", CreatedAt = Now.AddHours(-20), Text = "old" };

        var newest = Threads.Build([p1, p2, reply, like, follow, orphan], SortOrder.NewestFirst);
        Assert.Equal(["p1", "f1", "p2", "r2"], newest.Select(t => t.Post?.Id ?? t.Activity[0].Id));
        Assert.Equal(["l1", "r1"], newest[0].Activity.Select(i => i.Id));   // oldest first inside a thread
        Assert.Equal(2, newest[0].UnseenCount);

        var unread = Threads.Build([p1, p2, reply, like, follow, orphan], SortOrder.OldestUnreadFirst);
        Assert.Equal(["r2", "p1", "f1", "p2"], unread.Select(t => t.Post?.Id ?? t.Activity[0].Id));
    }

    [Fact]
    public void A_post_cached_under_two_accounts_shows_once()
    {
        var threads = Threads.Build([Post("ron", "x", 1, seen: true), Post("sarah", "x", 1)], SortOrder.NewestFirst);
        Assert.Single(threads);
        Assert.True(threads[0].HasUnseen);
    }
}
