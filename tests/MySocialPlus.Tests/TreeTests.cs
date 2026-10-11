using MySocialPlus.Core.Model;
using MySocialPlus.Core.Store;

namespace MySocialPlus.Tests;

public class TreeTests
{
    private static Account Bsky(string handle) => new() { Network = Network.Bluesky, Handle = handle };

    [Fact]
    public void Add_move_rename_delete()
    {
        var t = SocialTree.CreateNew();
        var family = TreeNode.Topic("Family");
        var work = TreeNode.Topic("Work");
        t.Add(t.MyFriends, family);
        t.Add(t.MyFriends, work);
        var sarah = TreeNode.Person("Sarah");
        t.Add(family, sarah);
        t.Add(sarah, TreeNode.ForAccount(Bsky("sarah.bsky.social")));

        Assert.Same(family, sarah.Parent);
        Assert.Equal(["bluesky:sarah.bsky.social"], family.AccountKeys());

        t.Move(sarah, work);
        Assert.Empty(family.Children);
        Assert.Same(work, sarah.Parent);
        Assert.Equal(["bluesky:sarah.bsky.social"], work.AccountKeys());

        SocialTree.Rename(sarah, "  Sarah Lee ");
        Assert.Equal("Sarah Lee", sarah.Name);

        t.Delete(work);
        Assert.Null(t.Find(sarah.Id));
        Assert.Single(t.MyFriends.Children);
    }

    [Fact]
    public void Move_within_the_same_parent_keeps_the_order_asked_for()
    {
        var t = SocialTree.CreateNew();
        var a = TreeNode.Topic("A"); var b = TreeNode.Topic("B"); var c = TreeNode.Topic("C");
        t.Add(t.MyFriends, a); t.Add(t.MyFriends, b); t.Add(t.MyFriends, c);
        t.Move(a, t.MyFriends, 3); // drop after C
        Assert.Equal(["B", "C", "A"], t.MyFriends.Children.Select(n => n.Name));
        t.Move(a, t.MyFriends, 0);
        Assert.Equal(["A", "B", "C"], t.MyFriends.Children.Select(n => n.Name));
    }

    [Fact]
    public void The_rules_hold()
    {
        var t = SocialTree.CreateNew();
        var topic = TreeNode.Topic("Family");
        t.Add(t.MyFriends, topic);
        var inner = TreeNode.Topic("Cousins");
        t.Add(topic, inner);

        Assert.Throws<InvalidOperationException>(() => t.Add(t.MyPosts, TreeNode.Person("Sarah")));         // people are friends
        Assert.Throws<InvalidOperationException>(() => t.Add(topic, TreeNode.ForAccount(Bsky("x.test"))));   // accounts go in people
        Assert.Throws<InvalidOperationException>(() => t.Move(topic, inner));                                // not inside itself
        Assert.Throws<InvalidOperationException>(() => t.Delete(t.MyPosts));
        Assert.Throws<InvalidOperationException>(() => SocialTree.Rename(t.MyFriends, "Pals"));
        Assert.Throws<InvalidOperationException>(() => SocialTree.Rename(topic, "   "));

        var mine = TreeNode.ForAccount(Bsky("ron.test"));
        t.Add(t.MyPosts, mine);
        Assert.True(mine.Account!.IsMine);
        var p = TreeNode.Person("Ron again");
        t.Add(topic, p);
        Assert.Throws<InvalidOperationException>(() => t.Add(p, TreeNode.ForAccount(Bsky("RON.test"))));     // one node per account
        Assert.Throws<InvalidOperationException>(() => t.Move(mine, p));                                    // own accounts stay in My Posts
    }

    [Fact]
    public void Round_trip_keeps_everything_including_fields_from_a_newer_version()
    {
        var t = SocialTree.CreateNew();
        var family = TreeNode.Topic("Family");
        t.Add(t.MyFriends, family);
        var sarah = TreeNode.Person("Sarah");
        sarah.Sort = SortOrder.OldestUnreadFirst;
        t.Add(family, sarah);
        t.Add(sarah, TreeNode.ForAccount(new Account { Network = Network.Facebook, Handle = "sarah.lee" }));

        var json = TreeStore.ToJson(t).Replace("\"name\": \"Sarah\"", "\"name\": \"Sarah\",\n \"colour\": \"teal\"", StringComparison.Ordinal);
        var back = TreeStore.FromJson(json);
        var again = TreeStore.ToJson(back);

        Assert.Contains("\"colour\": \"teal\"", again, StringComparison.Ordinal);
        var s = back.MyFriends.Children[0].Children[0];
        Assert.Equal("Sarah", s.Name);
        Assert.Equal(SortOrder.OldestUnreadFirst, s.Sort);
        Assert.Same(back.MyFriends.Children[0], s.Parent);
        Assert.Equal(Network.Facebook, s.Children[0].Account!.Network);
        Assert.Contains("\"network\": \"facebook\"", again, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_and_load_a_file()
    {
        var dir = Directory.CreateTempSubdirectory("msp-tree").FullName;
        try
        {
            var path = Path.Combine(dir, "tree.json");
            Assert.Equal(4, TreeStore.Load(path).Roots.Count);   // no file yet: a new tree
            var t = SocialTree.CreateNew();
            t.Add(t.MyFriends, TreeNode.Topic("Family"));
            TreeStore.Save(t, path);
            Assert.Equal("Family", TreeStore.Load(path).MyFriends.Children[0].Name);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData("www.facebook.com/jane.sample.42", "jane.sample.42")]
    [InlineData("https://www.facebook.com/jane.sample.42/", "jane.sample.42")]
    [InlineData("facebook.com/jane.sample.42?ref=bookmarks", "jane.sample.42")]
    [InlineData("https://m.facebook.com/jane.sample.42", "jane.sample.42")]
    [InlineData("jane.sample.42", "jane.sample.42")]
    [InlineData("https://www.facebook.com/profile.php?id=100012345", "https://www.facebook.com/profile.php?id=100012345")]
    [InlineData("www.facebook.com/people/Jane-Doe/100012345/", "https://www.facebook.com/people/Jane-Doe/100012345/")]
    [InlineData("https://www.facebook.com/share/abc123/", "https://www.facebook.com/share/abc123/")]
    public void A_pasted_Facebook_address_becomes_the_handle(string pasted, string handle)
    {
        var fb = NetworkInfo.For(Network.Facebook);
        var h = fb.NormalizeHandle(pasted);
        Assert.Equal(handle, h);
        Assert.StartsWith("https://www.facebook.com/", fb.ProfileUrl(h), StringComparison.Ordinal);
    }

    [Fact]
    public void Other_networks_take_pasted_addresses_too()
    {
        Assert.Equal("sarah.bsky.social", NetworkInfo.For(Network.Bluesky).NormalizeHandle("https://bsky.app/profile/sarah.bsky.social"));
        Assert.Equal("someone", NetworkInfo.For(Network.Instagram).NormalizeHandle("instagram.com/someone/"));
        Assert.Equal("https://mastodon.social/@ann", NetworkInfo.For(Network.Mastodon).NormalizeHandle("mastodon.social/@ann"));
    }

    [Fact]
    public void An_unreadable_network_has_no_account_keys_so_no_badge()
    {
        var t = SocialTree.CreateNew();
        var p = TreeNode.Person("Jim");
        t.Add(t.MyFriends, p);
        t.Add(p, TreeNode.ForAccount(new Account { Network = Network.Facebook, Handle = "jim" }));
        Assert.Empty(p.AccountKeys());
        Assert.False(p.Children[0].Account!.IsReadable);
        Assert.Contains("Facebook does not let", p.Children[0].Account!.Info.WhyNot, StringComparison.Ordinal);
        Assert.Equal("https://www.facebook.com/jim", p.Children[0].Account!.ProfileUrl);
    }
}
