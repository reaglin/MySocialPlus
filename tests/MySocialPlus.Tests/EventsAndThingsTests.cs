using MySocialPlus.Core.Content;
using MySocialPlus.Core.Model;
using MySocialPlus.Core.Store;

namespace MySocialPlus.Tests;

public class EventsAndThingsTests
{
    private static readonly DateOnly Today = new(2026, 10, 11);

    [Fact]
    public void A_new_tree_has_four_headings_and_an_old_file_gets_the_new_two()
    {
        Assert.Equal(["My Posts", "My Friends", "My Events", "My Things"], SocialTree.CreateNew().Roots.Select(r => r.Name));

        var old = """
            { "version": 1, "roots": [ { "id": "my-posts", "kind": "root", "name": "My Posts" },
              { "id": "my-friends", "kind": "root", "name": "My Friends", "children": [ { "kind": "person", "name": "Sarah" } ] } ] }
            """;
        var t = TreeStore.FromJson(old);
        Assert.Equal(["My Posts", "My Friends", "My Events", "My Things"], t.Roots.Select(r => r.Name));
        Assert.Equal("Sarah", t.MyFriends.Children[0].Name);
    }

    [Fact]
    public void Events_and_things_go_under_their_own_heading()
    {
        var t = SocialTree.CreateNew();
        var running = TreeNode.Topic("Running");
        t.Add(t.MyEvents, running);
        t.Add(running, TreeNode.NewEvent("Turkey Trot"));
        var vehicles = TreeNode.Topic("Vehicles");
        t.Add(t.MyThings, vehicles);
        t.Add(vehicles, TreeNode.Thing("Camry"));

        Assert.Throws<InvalidOperationException>(() => t.Add(t.MyFriends, TreeNode.Thing("Toaster")));
        Assert.Throws<InvalidOperationException>(() => t.Add(t.MyThings, TreeNode.NewEvent("Party")));
        Assert.Throws<InvalidOperationException>(() => t.Add(t.MyEvents, TreeNode.Person("Sam")));
        Assert.Throws<InvalidOperationException>(() => t.Move(vehicles, t.MyFriends));   // a topic keeps to its heading
        t.Move(running, t.MyEvents);                                                     // within it is fine
    }

    [Fact]
    public void Details_templates_and_logs_round_trip_and_empty_lists_are_left_out()
    {
        var t = SocialTree.CreateNew();
        var car = TreeNode.Thing("Camry");
        TemplateStore.Shipped().Find("car", TemplateFor.Thing)!.ApplyTo(car);
        t.Add(t.MyThings, car);
        car.Set("License plate", "ABC 123");
        car.Set("Registration renews", "2026-10-20", DetailKind.Date);
        car.Set("Favorite road trip", "Blue Ridge Parkway");                     // not in the template: just added
        car.Log.Add(new LogEntry { Date = new DateOnly(2026, 9, 2), Text = "Oil change", Cost = "89.95" });

        var json = TreeStore.ToJson(t);
        var back = TreeStore.FromJson(json).MyThings.Children[0];
        Assert.Equal("Car", back.TypeName);
        Assert.Equal("ABC 123", back.Get("license plate"));
        Assert.Equal("Blue Ridge Parkway", back.Get("Favorite road trip"));
        Assert.True(back.Details.First(d => d.Name == "Registration renews").Remind);
        Assert.Equal("Oil change", Assert.Single(back.Log).Text);
        Assert.DoesNotContain("\"details\": []", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"log\": []", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Applying_a_template_twice_or_another_one_adds_only_what_is_missing()
    {
        var n = TreeNode.Thing("Kitchen thing");
        n.Set("Brand", "Breville");
        var set = TemplateStore.Shipped();
        set.Find("Coffee maker", TemplateFor.Thing)!.ApplyTo(n);
        var count = n.Details.Count;
        set.Find("Coffee maker", TemplateFor.Thing)!.ApplyTo(n);
        Assert.Equal(count, n.Details.Count);
        Assert.Equal("Breville", n.Get("Brand"));
        Assert.Equal("Coffee maker", n.TypeName);
    }

    [Fact]
    public void The_shipped_templates_cover_people_events_and_things_and_people_have_a_photos_link()
    {
        var set = TemplateStore.Shipped();
        Assert.True(set.Version >= 1);
        Assert.Contains(set.For(TemplateFor.Event), x => x.Name == "Race");
        Assert.Contains(set.For(TemplateFor.Event), x => x.Name == "Yoga or fitness class");
        Assert.Contains(set.For(TemplateFor.Thing), x => x.Name == "House");
        Assert.Equal(DetailKind.Link, set.Find("Friend", TemplateFor.Person)!.Fields.Single(f => f.Name == "Google Photos").Kind);
        Assert.All(set.Templates, x => Assert.Equal(x.Fields.Count, x.Fields.Select(f => f.Name.ToUpperInvariant()).Distinct().Count()));
    }

    [Fact]
    public void Template_file_is_written_once_then_Rons_and_newer_kinds_are_added()
    {
        var dir = Directory.CreateTempSubdirectory("msp-templates").FullName;
        try
        {
            var path = Path.Combine(dir, "templates.json");
            var first = TemplateStore.Load(path);
            Assert.True(File.Exists(path));

            // Ron removes one and adds his own; an older version number means a newer program has more.
            first.Templates.RemoveAll(x => x.Name == "Boat");
            first.Templates.Add(new Template { Name = "Kayak", For = TemplateFor.Thing, Fields = [new FieldDef { Name = "Length" }] });
            first.Version = 0;
            TemplateStore.Save(first, path);

            var again = TemplateStore.Load(path);
            Assert.NotNull(again.Find("Kayak", TemplateFor.Thing));
            Assert.NotNull(again.Find("Boat", TemplateFor.Thing));        // came back: the file was older than the program
            first.Templates.RemoveAll(x => x.Name == "Boat");
            first.Version = again.Version;
            TemplateStore.Save(first, path);
            Assert.Null(TemplateStore.Load(path).Find("Boat", TemplateFor.Thing));   // now his choice stands
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Save_as_a_new_kind_copies_the_detail_names_not_the_values()
    {
        var n = TreeNode.Thing("Kayak");
        n.Set("Length", "12 ft");
        n.Set("Paddle", "Werner");
        var k = Template.From(n, "Kayak", TemplateFor.Thing, "Trips");
        Assert.Equal(["Length", "Paddle"], k.Fields.Select(f => f.Name));
        var fresh = TreeNode.Thing("Other kayak");
        k.ApplyTo(fresh);
        Assert.All(fresh.Details, d => Assert.Equal("", d.Value));
    }

    [Fact]
    public void Repeating_events_find_their_next_date()
    {
        var e = new EventInfo { Start = new DateTime(2026, 9, 1, 18, 0, 0), Repeat = Repeat.Weekly };   // a Tuesday
        Assert.Equal(new DateTime(2026, 10, 13, 18, 0, 0), e.NextOn(Today));
        e.Repeat = Repeat.Monthly;
        Assert.Equal(new DateTime(2026, 11, 1, 18, 0, 0), e.NextOn(Today));
        e.Repeat = Repeat.None;
        Assert.Null(e.NextOn(Today));                                                           // it was in the past
        e.Start = new DateTime(2026, 10, 18, 7, 30, 0);
        Assert.Equal(e.Start, e.NextOn(Today));
    }

    [Fact]
    public void Birthdays_with_and_without_a_year_and_on_29_February()
    {
        Assert.Equal(new DateOnly(1984, 10, 15), Dates.Parse("1984-10-15"));
        var noYear = Dates.Parse("--10-15")!.Value;
        Assert.False(Dates.HasYear(noYear));
        Assert.Equal(new DateOnly(2026, 10, 15), Dates.NextAnniversary(noYear, Today));
        Assert.Equal(new DateOnly(2027, 2, 28), Dates.NextAnniversary(new DateOnly(2000, 2, 29), Today));
        Assert.Equal("--10-15", Dates.Format(noYear, knownYear: false));
    }

    [Fact]
    public void Coming_up_lists_events_birthdays_and_reminders_in_date_order()
    {
        var t = SocialTree.CreateNew();
        var sarah = TreeNode.Person("Sarah Lee");
        t.Add(t.MyFriends, sarah);
        sarah.Set("Birthday", "1984-10-15", DetailKind.Birthday);
        var tom = TreeNode.Person("Tom");
        t.Add(t.MyFriends, tom);
        tom.Set("Birthday", "--12-25", DetailKind.Birthday);                     // outside 30 days

        var race = TreeNode.NewEvent("Turkey Trot 5K", new DateTime(2026, 10, 18, 7, 30, 0));
        race.Event!.Location = "Riverside Park";
        race.Event.People.Add(new EventPerson { PersonId = sarah.Id, Name = "Sarah Lee" });
        t.Add(t.MyEvents, race);
        var went = TreeNode.NewEvent("Old concert", new DateTime(2026, 10, 12, 20, 0, 0));
        went.Event!.Status = EventStatus.Went;
        t.Add(t.MyEvents, went);

        var car = TreeNode.Thing("Camry");
        t.Add(t.MyThings, car);
        car.Set("Registration renews", "2026-10-09", DetailKind.Date).Remind = true;  // 2 days overdue
        car.Set("Purchased", "2026-10-12", DetailKind.Date);                          // a date, but not a reminder

        var list = ComingUp.Build(t, Today);
        Assert.Equal(["Camry: Registration renews", "Sarah Lee's birthday", "Turkey Trot 5K"], list.Select(i => i.Title));
        Assert.True(list[0].IsOverdue(Today));
        Assert.Equal("turns 42", list[1].Detail);
        Assert.Equal("Riverside Park · with Sarah Lee", list[2].Detail);
        Assert.Equal("2 days ago", ComingUp.When(list[0], Today));
        Assert.Single(t.EventsWith(sarah));
    }

    [Fact]
    public void A_team_event_lists_how_to_reach_everyone()
    {
        var t = SocialTree.CreateNew();
        TreeNode Friend(string name, string? email, string? phone)
        {
            var p = TreeNode.Person(name);
            t.Add(t.MyFriends, p);
            if (email is not null) p.Set("Email", email, DetailKind.Email);
            if (phone is not null) p.Set("Mobile", phone, DetailKind.Phone);
            return p;
        }
        var a = Friend("Ann", "ann@example.com", "555-0101");
        var b = Friend("Bo", "bo@example.com", null);
        var race = TreeNode.NewEvent("Nationals");
        t.Add(t.MyEvents, race);
        race.Event!.People.Add(new EventPerson { PersonId = a.Id, Name = "Ann" });
        race.Event.People.Add(new EventPerson { PersonId = b.Id, Name = "Bo" });
        race.Event.People.Add(new EventPerson { Name = "Coach" });            // not in the tree
        race.Event.People.Add(new EventPerson { Name = "Pat", Email = "pat@example.com", Phone = "555-0199" });   // not a friend, but reachable

        var c = Contacts.For(t, race);
        Assert.Equal("ann@example.com; bo@example.com; pat@example.com", Contacts.EmailList(c));
        Assert.Equal("555-0101, 555-0199", Contacts.PhoneList(c));
        Assert.Equal("mailto:ann%40example.com,bo%40example.com,pat%40example.com?subject=Nationals", Contacts.MailTo(c, "Nationals"));
        Assert.Equal(["Bo", "Coach"], Contacts.Missing(c, DetailKind.Phone));

        SocialTree.Rename(a, "Ann Lee");                                     // the tree's name wins over the saved one
        Assert.Equal("Ann Lee", Contacts.For(t, race)[0].Name);
    }

    [Fact]
    public void The_sample_has_events_things_and_coming_up()
    {
        var t = Core.Sample.SampleData.BuildTree(new DateTimeOffset(Today.ToDateTime(new TimeOnly(9, 0))));
        Assert.Contains(t.MyEvents.SelfAndDescendants(), n => n.Kind == NodeKind.Event);
        Assert.Contains(t.MyThings.SelfAndDescendants(), n => n.Kind == NodeKind.Thing);
        var list = ComingUp.Build(t, Today);
        Assert.Contains(list, i => i.Kind == ComingUpKind.Birthday);
        Assert.Contains(list, i => i.Title == "Turkey Trot 5K");
        Assert.Contains(list, i => i.Title == "Our house: Next filter change" && i.IsOverdue(Today));
        var ar = t.AllNodes().First(n => n.Name == "Nationals Adventure Race");
        Assert.Equal(5, ar.Event!.Agenda.Count);
        Assert.Equal(3, Contacts.For(t, ar).Count);
    }

    [Fact]
    public void An_event_becomes_a_calendar_file()
    {
        var race = TreeNode.NewEvent("Turkey Trot 5K, with pie", new DateTime(2026, 11, 26, 7, 30, 0));
        race.Event!.Location = "Riverside Park";
        race.Event.People.Add(new EventPerson { Name = "Sarah Lee" });
        race.Set("Bib number", "1042");
        race.Event.Agenda.Add(new AgendaItem { At = new DateTime(2026, 11, 25, 17, 0, 0), Text = "Packet pickup", Where = "Running store" });
        var ics = Ics.For(race, new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc));
        Assert.Contains("Packet pickup (Running store)", ics.Replace("\r\n ", "", StringComparison.Ordinal), StringComparison.Ordinal);

        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTSTART:20261126T073000\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTEND:20261126T083000\r\n", ics, StringComparison.Ordinal);       // an hour when no end is given
        Assert.Contains("SUMMARY:Turkey Trot 5K\\, with pie\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("With: Sarah Lee\\nWed 5:00 PM — Packet pickup (Running store)\\nBib number: 1042",
                        ics.Replace("\r\n ", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.All(ics.Split("\r\n"), line => Assert.True(System.Text.Encoding.UTF8.GetByteCount(line) <= 75));
        Assert.Throws<InvalidOperationException>(() => Ics.For(TreeNode.NewEvent("No date"), DateTime.UtcNow));
    }
}
