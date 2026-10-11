using MySocialPlus.Core.Model;
using MySocialPlus.Core.Store;

namespace MySocialPlus.Core.Sample;

/// <summary>
/// A made-up tree and cache — family, colleagues, old friends, Ron's own posts with replies and
/// likes — so the window can be built and checked without connecting a network (plan 1.5). The
/// people are invented; the accounts point nowhere, and the sample never reads a network.
/// </summary>
public static class SampleData
{
    public static void Write(DataPaths paths, DateTimeOffset now)
    {
        Directory.CreateDirectory(paths.DataFolder);
        if (File.Exists(paths.ItemsDb)) File.Delete(paths.ItemsDb);
        var tree = BuildTree(now);
        TreeStore.Save(tree, paths.TreeFile);
        using var cache = new ItemCache(paths.ItemsDb);
        cache.Upsert(BuildItems(tree, now));
    }

    private static Account Bsky(string handle, string did, DateTimeOffset now, bool mine = false) =>
        new() { Network = Network.Bluesky, Handle = handle, Id = did, IsMine = mine, LastChecked = now.AddMinutes(-4) };

    public static SocialTree BuildTree(DateTimeOffset now)
    {
        var t = SocialTree.CreateNew();
        t.Add(t.MyPosts, TreeNode.ForAccount(Bsky("roneaglin.example", "did:sample:ron", now, mine: true)));
        t.Add(t.MyPosts, TreeNode.ForAccount(new Account { Network = Network.Facebook, Handle = "ron.eaglin.sample", IsMine = true }));

        var family = TreeNode.Topic("Family");
        var colleagues = TreeNode.Topic("Colleagues");
        var old = TreeNode.Topic("Old friends");
        t.Add(t.MyFriends, family);
        t.Add(t.MyFriends, colleagues);
        t.Add(t.MyFriends, old);

        TreeNode Person(TreeNode topic, string name, params Account[] accounts)
        {
            var p = TreeNode.Person(name);
            t.Add(topic, p);
            foreach (var a in accounts) t.Add(p, TreeNode.ForAccount(a));
            return p;
        }

        Person(family, "Sarah Lee", Bsky("sarahlee.example", "did:sample:sarah", now),
               new Account { Network = Network.Facebook, Handle = "sarah.lee.sample" });
        Person(family, "Tom Eaglin", Bsky("tomeaglin.example", "did:sample:tom", now));
        Person(colleagues, "Priya Natarajan", Bsky("priya.example", "did:sample:priya", now));
        Person(colleagues, "Marcus Webb", Bsky("marcuswebb.example", "did:sample:marcus", now));
        Person(colleagues, "Dana Ortiz", Bsky("danaortiz.example", "did:sample:dana", now));
        Person(old, "Jim Halloran", new Account { Network = Network.Facebook, Handle = "jim.halloran.sample" });
        Person(old, "Ann Chu", Bsky("annchu.example", "did:sample:ann", now));
        return t;
    }

    public static List<Item> BuildItems(SocialTree tree, DateTimeOffset now)
    {
        var items = new List<Item>();
        string Key(string did) => tree.Accounts().First(a => a.Id == did).Key;
        var n = 0;

        Item Post(string did, string author, string handle, double hoursAgo, string text, bool seen,
                  int likes = 0, int replies = 0, string? attachment = null, string? context = null)
        {
            var i = new Item
            {
                AccountKey = Key(did), Network = Network.Bluesky, Id = $"at://{did}/app.bsky.feed.post/sample{n++}",
                Kind = ItemKind.Post, AuthorName = author, AuthorHandle = handle,
                CreatedAt = now.AddHours(-hoursAgo), FetchedAt = now, Text = text, Seen = seen,
                Likes = likes, Replies = replies, Attachment = attachment, Context = context,
                Link = "https://bsky.app/",
            };
            items.Add(i);
            return i;
        }

        void On(Item post, ItemKind kind, string who, double hoursAgo, string text, bool seen)
        {
            items.Add(new Item
            {
                AccountKey = post.AccountKey, Network = post.Network, Id = $"{post.Id}/activity{n++}",
                Kind = kind, ParentId = post.Id, AuthorName = who, AuthorHandle = who.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant() + ".example",
                CreatedAt = now.AddHours(-hoursAgo), FetchedAt = now, Text = text, Seen = seen,
            });
        }

        // My Posts ▸ Bluesky: Ron's posts, with new replies and likes on one of them.
        var ron1 = Post("did:sample:ron", "Ron Eaglin", "roneaglin.example", 20,
            "New video for EGN3443: confidence intervals, worked by hand and then in Python and R.", seen: true, likes: 12, replies: 3);
        On(ron1, ItemKind.Comment, "Priya Natarajan", 18, "Sending this to my section — thank you!", seen: true);
        On(ron1, ItemKind.Reaction, "Tom Eaglin", 3, "liked this", seen: false);
        On(ron1, ItemKind.Comment, "Sarah Lee", 2, "Love this — when is the next class?", seen: false);
        On(ron1, ItemKind.Repost, "Dana Ortiz", 1.5, "reposted this", seen: false);
        var ron2 = Post("did:sample:ron", "Ron Eaglin", "roneaglin.example", 50,
            "TrafficLab+ is on the Microsoft Store: turn a real intersection into a live traffic simulation.", seen: true, likes: 30,
            attachment: "TrafficLab+ — https://softwareplus.ai/trafficlab/");
        On(ron2, ItemKind.Reaction, "Marcus Webb", 40, "liked this", seen: true);
        items.Add(new Item
        {
            AccountKey = Key("did:sample:ron"), Network = Network.Bluesky, Id = "sample-follow-1", Kind = ItemKind.Follow,
            AuthorName = "Lena Fischer", AuthorHandle = "lena.example", CreatedAt = now.AddHours(-5), FetchedAt = now,
            Text = "followed you", Seen = false,
        });

        // My Friends.
        Post("did:sample:sarah", "Sarah Lee", "sarahlee.example", 2.2, "Finally finished the garden path. Three weekends, one sore back.", seen: false, likes: 8);
        Post("did:sample:sarah", "Sarah Lee", "sarahlee.example", 26, "Farmers market haul 🍅", seen: true, likes: 15);
        Post("did:sample:tom", "Tom Eaglin", "tomeaglin.example", 6, "Boat's back in the water. Who's in for Saturday?", seen: false, replies: 4);
        Post("did:sample:priya", "Priya Natarajan", "priya.example", 1, "Our paper on stormwater BMPs was accepted!", seen: false, likes: 40);
        Post("did:sample:marcus", "Marcus Webb", "marcuswebb.example", 9, "Anyone have a good intro text for linear regression?", seen: false, replies: 11);
        Post("did:sample:marcus", "Marcus Webb", "marcuswebb.example", 30, "Grading week. Send coffee.", seen: true);
        Post("did:sample:dana", "Dana Ortiz", "danaortiz.example", 4, "Conference slides are up.", seen: false,
             attachment: "Quoting Ron Eaglin: New video for EGN3443…", context: "Reposted by Dana Ortiz");
        Post("did:sample:ann", "Ann Chu", "annchu.example", 70, "Moved to Asheville! Mountains everywhere.", seen: true, likes: 22);
        return items;
    }
}
