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

        var sarah = Person(family, "Sarah Lee", Bsky("sarahlee.example", "did:sample:sarah", now),
               new Account { Network = Network.Facebook, Handle = "sarah.lee.sample" });
        var tom = Person(family, "Tom Eaglin", Bsky("tomeaglin.example", "did:sample:tom", now));
        Person(colleagues, "Priya Natarajan", Bsky("priya.example", "did:sample:priya", now));
        Person(colleagues, "Marcus Webb", Bsky("marcuswebb.example", "did:sample:marcus", now));
        Person(colleagues, "Dana Ortiz", Bsky("danaortiz.example", "did:sample:dana", now));
        Person(old, "Jim Halloran", new Account { Network = Network.Facebook, Handle = "jim.halloran.sample" });
        Person(old, "Ann Chu", Bsky("annchu.example", "did:sample:ann", now));
        // Someone with no social media at all: MySocial+ works without it (Ron, 2026-10-11).
        var manny = Person(old, "Manny Otero");

        var templates = TemplateStore.Shipped();
        var today = DateOnly.FromDateTime(now.LocalDateTime);
        string Day(int days) => Dates.Format(today.AddDays(days));

        templates.Find("Family member", TemplateFor.Person)!.ApplyTo(sarah);
        sarah.Set("Relationship", "Sister");
        sarah.Set("Birthday", Dates.Format(new DateOnly(1984, today.AddDays(4).Month, today.AddDays(4).Day)));
        sarah.Set("Partner", "David");
        sarah.Set("Children", "Maya (9), Leo (6)");
        sarah.Set("Likes", "Gardening, mystery novels, dark chocolate, the Rays");
        sarah.Set("Gift ideas", "A good pair of pruning shears — she mentioned hers broke");
        sarah.Log.Add(new LogEntry { Date = today.AddDays(-12), Text = "Called — Maya started piano lessons; Leo lost his first tooth." });

        templates.Find("Friend", TemplateFor.Person)!.ApplyTo(manny);
        manny.Set("Birthday", "--" + today.AddDays(19).ToString("MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        manny.Set("Likes", "Fishing, Cuban coffee, old Corvettes");
        manny.Set("How we met", "Worked together at the college, 2009");
        manny.Set("Google Photos", "https://photos.google.com/search/_sample_");
        templates.Find("Family member", TemplateFor.Person)!.ApplyTo(tom);
        tom.Set("Relationship", "Brother");

        // My Events.
        var running = TreeNode.Topic("Running");
        var fun = TreeNode.Topic("Going out");
        t.Add(t.MyEvents, running);
        t.Add(t.MyEvents, fun);
        var at = today.ToDateTime(TimeOnly.MinValue);
        var race = TreeNode.NewEvent("Turkey Trot 5K", at.AddDays(6).AddHours(7.5));
        templates.Find("Race", TemplateFor.Event)!.ApplyTo(race);
        race.Event!.Location = "Riverside Park";
        race.Event.Status = EventStatus.Going;
        race.Event.People.Add(new EventPerson { PersonId = sarah.Id, Name = sarah.Name });
        race.Event.People.Add(new EventPerson { Name = "Coach Rivera" });
        race.Set("Distance", "5K");
        race.Set("Packet pickup", Day(5));
        race.Set("Goal time", "28:00");
        t.Add(running, race);
        var yoga = TreeNode.NewEvent("Tuesday yoga", at.AddDays(-14 + (int)DayOfWeek.Tuesday - (int)at.DayOfWeek).AddHours(18));
        templates.Find("Yoga or fitness class", TemplateFor.Event)!.ApplyTo(yoga);
        yoga.Event!.Repeat = Repeat.Weekly;
        yoga.Event.Location = "Sun Studio";
        yoga.Event.Status = EventStatus.Going;
        yoga.Set("Instructor", "Kim");
        yoga.Set("Bring", "Mat, water");
        t.Add(running, yoga);
        // A team event: the plan, the team from My Friends, and how to reach them all.
        sarah.Set("Email", "sarah@example.com", DetailKind.Email);
        sarah.Set("Phone", "555-0142", DetailKind.Phone);
        manny.Set("Email", "manny@example.com", DetailKind.Email);
        manny.Set("Phone", "555-0177", DetailKind.Phone);
        var dana = t.People().First(p => p.Name == "Dana Ortiz");
        templates.Find("Colleague", TemplateFor.Person)!.ApplyTo(dana);
        dana.Set("Email", "dana@example.com", DetailKind.Email);
        var ar = TreeNode.NewEvent("Nationals Adventure Race", at.AddDays(26).AddHours(6));
        templates.Find("Adventure race", TemplateFor.Event)!.ApplyTo(ar);
        ar.Event!.End = at.AddDays(27).AddHours(18);
        ar.Event.Location = "Sample State Park";
        ar.Event.Status = EventStatus.Going;
        foreach (var p in new[] { sarah, manny, dana }) ar.Event.People.Add(new EventPerson { PersonId = p.Id, Name = p.Name });
        ar.Set("Team name", "Sample Mudskippers");
        ar.Set("Division", "4-person coed");
        ar.Set("Team gear", "Sarah: maps case and compass. Manny: canoe and paddles. Dana: first-aid kit.");
        ar.Event.Agenda.Add(new AgendaItem { At = at.AddDays(25).AddHours(16), Text = "Check-in and gear check", Where = "Race HQ lodge" });
        ar.Event.Agenda.Add(new AgendaItem { At = at.AddDays(25).AddHours(19), Text = "Pre-race meeting; maps handed out" });
        ar.Event.Agenda.Add(new AgendaItem { At = at.AddDays(26).AddHours(6), Text = "Start — trek to the first checkpoint", Where = "Boat ramp" });
        ar.Event.Agenda.Add(new AgendaItem { At = at.AddDays(26).AddHours(13), Text = "Paddle leg cut-off at CP4" });
        ar.Event.Agenda.Add(new AgendaItem { At = at.AddDays(27).AddHours(18), Text = "Finish cut-off and awards" });
        t.Add(running, ar);

        var concert = TreeNode.NewEvent("Jazz on the River", at.AddDays(12).AddHours(19.5));
        templates.Find("Concert", TemplateFor.Event)!.ApplyTo(concert);
        concert.Event!.Location = "Riverfront Amphitheater";
        concert.Event.People.Add(new EventPerson { PersonId = tom.Id, Name = tom.Name });
        concert.Set("Seats", "Lawn — bring chairs");
        t.Add(fun, concert);

        // My Things.
        var vehicles = TreeNode.Topic("Vehicles");
        var home = TreeNode.Topic("Home");
        t.Add(t.MyThings, vehicles);
        t.Add(t.MyThings, home);
        var car = TreeNode.Thing("Camry");
        templates.Find("Car", TemplateFor.Thing)!.ApplyTo(car);
        car.Set("Year", "2019"); car.Set("Make", "Toyota"); car.Set("Model", "Camry"); car.Set("Trim", "SE");
        car.Set("License plate", "SAMPLE 1"); car.Set("Registration renews", Day(9)); car.Set("Mileage", "61,240");
        car.Set("Oil type", "0W-20 synthetic");
        car.Log.Add(new LogEntry { Date = today.AddDays(-40), Text = "Oil change, tire rotation", Cost = "89.95" });
        car.Log.Add(new LogEntry { Date = today.AddDays(-200), Text = "New front brake pads", Cost = "310.00" });
        t.Add(vehicles, car);
        var house = TreeNode.Thing("Our house");
        templates.Find("House", TemplateFor.Thing)!.ApplyTo(house);
        house.Set("Air filter size", "16x25x1");
        house.Set("Next filter change", Day(-2));
        house.Set("Water shutoff", "Garage, left of the water heater");
        t.Add(home, house);
        var coffee = TreeNode.Thing("Coffee maker");
        templates.Find("Coffee maker", TemplateFor.Thing)!.ApplyTo(coffee);
        coffee.Set("Brand", "Breville"); coffee.Set("Model", "Barista Express"); coffee.Set("Next descaling", Day(20));
        t.Add(home, coffee);
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
                Kind = kind, ParentId = post.Id, AuthorName = who, Link = "https://bsky.app/", AuthorHandle = who.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant() + ".example",
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
