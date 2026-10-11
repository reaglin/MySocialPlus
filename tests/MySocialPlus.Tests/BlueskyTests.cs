using System.Net;
using System.Text.Json.Nodes;
using MySocialPlus.Core.Model;
using MySocialPlus.Core.Secrets;
using MySocialPlus.Core.Sources;
using MySocialPlus.Core.Sources.Bluesky;

namespace MySocialPlus.Tests;

public class BlueskyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 16, 0, 0, TimeSpan.Zero);

    internal static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", name));

    [Fact]
    public void Author_feed_reposts_say_who_reposted_and_carry_their_time()
    {
        var items = BlueskyMapper.FromAuthorFeed(JsonNode.Parse(Fixture("bluesky-author-feed.json")), "bluesky:did:x", Now, mine: false);
        Assert.Equal(4, items.Count);
        var first = items[0];
        Assert.Equal(ItemKind.Post, first.Kind);
        Assert.Equal("Nick Sousanis", first.AuthorName);
        Assert.Equal("Reposted by Bluesky", first.Context);
        Assert.Contains("/app.bsky.feed.repost/", first.Id, StringComparison.Ordinal);
        Assert.Equal(DateTimeOffset.Parse("2026-10-11T16:59:27.855Z", System.Globalization.CultureInfo.InvariantCulture), first.CreatedAt);
        Assert.StartsWith("https://video.bsky.app/", first.ImageUrl, StringComparison.Ordinal);
        Assert.Equal("https://bsky.app/profile/nsousanis.bsky.social/post/3mxk7gsrfxs2w", first.Link);
        Assert.True(first.Likes > 0);
        Assert.All(items, i => Assert.False(i.Seen));
    }

    [Fact]
    public void Author_feed_with_pictures_link_cards_and_quotes()
    {
        var items = BlueskyMapper.FromAuthorFeed(JsonNode.Parse(Fixture("bluesky-author-feed-mixed.json")), "k", Now, mine: true);
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.True(i.Seen));                               // Ron's own posts are not news to him
        Assert.Contains(items, i => i.ImageUrl?.Contains("feed_thumbnail", StringComparison.Ordinal) == true);
        Assert.Contains(items, i => i.Attachment?.StartsWith("Quoting ", StringComparison.Ordinal) == true);
        Assert.Contains(items, i => i.Attachment?.Contains("https://en.wikipedia.org/", StringComparison.Ordinal) == true);
        Assert.All(items, i => Assert.False(string.IsNullOrEmpty(i.AuthorHandle)));
    }

    [Fact]
    public void Notifications_become_activity_on_Rons_posts()
    {
        var items = BlueskyMapper.FromNotifications(JsonNode.Parse(Fixture("bluesky-notifications.json")), "ron", Now);
        Assert.Equal(5, items.Count);                                              // the starter-pack one is skipped

        var reply = items[0];
        Assert.Equal(ItemKind.Comment, reply.Kind);
        Assert.Equal("at://did:plc:ronfixture/app.bsky.feed.post/3ronpost1", reply.ParentId);
        Assert.Equal("Love this — when is the next class?", reply.Text);
        Assert.Equal("Sarah Lee", reply.AuthorName);
        Assert.Equal("https://bsky.app/profile/sarahlee.bsky.social/post/3reply1", reply.Link);

        Assert.Equal(ItemKind.Reaction, items[1].Kind);
        Assert.Equal("liked this", items[1].Text);
        Assert.Equal(ItemKind.Repost, items[2].Kind);
        Assert.Equal("ann.example.com", items[2].AuthorOrHidden);                 // blank display name → the handle
        Assert.Equal(ItemKind.Follow, items[3].Kind);
        Assert.Null(items[3].ParentId);
        Assert.Equal(ItemKind.Mention, items[4].Kind);
        Assert.All(items, i => Assert.False(i.Seen));                              // Bluesky's isRead is ignored (rule 3)
    }

    [Fact]
    public void Follows_and_cursor()
    {
        var (people, cursor) = BlueskyMapper.FromFollows(JsonNode.Parse(Fixture("bluesky-follows.json")));
        Assert.Equal(5, people.Count);
        Assert.Equal("EMILY WILSON", people[0].Name);
        Assert.StartsWith("did:plc:", people[0].Did, StringComparison.Ordinal);
        Assert.NotNull(cursor);
    }

    [Fact]
    public void Nonsense_is_skipped_not_a_crash()
    {
        Assert.Empty(BlueskyMapper.FromAuthorFeed(JsonNode.Parse("""{"feed":[{"post":{"uri":5}},{},null]}"""), "k", Now, false));
        Assert.Empty(BlueskyMapper.FromNotifications(JsonNode.Parse("""{"notifications":"no"}"""), "k", Now));
        Assert.Empty(BlueskyMapper.FromFollows(null).People);
    }

    // ---- the source, against a fake network ------------------------------------------------

    private sealed class FakeBluesky : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage?>? Override { get; set; }
        public int SessionsMade { get; private set; }
        public bool ExpireNextAuthedCall { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var path = req.RequestUri!.AbsolutePath;
            Calls.Add(req.RequestUri.Host + path);
            if (Override?.Invoke(req) is { } custom) return Task.FromResult(custom);
            return Task.FromResult(path switch
            {
                "/xrpc/app.bsky.actor.getProfile" => Ok(Fixture("bluesky-profile.json")),
                "/xrpc/app.bsky.feed.getAuthorFeed" => Ok(Fixture("bluesky-author-feed.json")),
                "/xrpc/app.bsky.feed.getPosts" => Ok("""{"posts":[{"uri":"at://did:plc:ronfixture/app.bsky.feed.post/3ronpost1","author":{"did":"did:plc:ronfixture","handle":"ron.test"},"record":{"text":"my old post","createdAt":"2026-10-01T00:00:00Z"}}]}"""),
                "/xrpc/com.atproto.server.createSession" => Session(),
                "/xrpc/com.atproto.server.refreshSession" => Session(),
                "/xrpc/app.bsky.notification.listNotifications" => Notifications(req),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            });
        }

        private HttpResponseMessage Session()
        {
            SessionsMade++;
            return Ok($$$"""
                {"did":"did:plc:ronfixture","handle":"ron.test","accessJwt":"access{{{SessionsMade}}}","refreshJwt":"refresh",
                 "didDoc":{"service":[{"id":"#atproto_pds","type":"AtprotoPersonalDataServer","serviceEndpoint":"https://pds.example.test"}]}}
                """);
        }

        private HttpResponseMessage Notifications(HttpRequestMessage req)
        {
            if (ExpireNextAuthedCall)
            {
                ExpireNextAuthedCall = false;
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":"ExpiredToken","message":"Token has expired"}""") };
            }
            Assert.StartsWith("access", req.Headers.Authorization!.Parameter, StringComparison.Ordinal);
            return Ok(Fixture("bluesky-notifications.json"));
        }

        private static HttpResponseMessage Ok(string json) =>
            new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
    }

    private static (BlueskySource Source, FakeBluesky Net, SecretStore Secrets, string Dir) Make()
    {
        var net = new FakeBluesky();
        var dir = Directory.CreateTempSubdirectory("msp-secrets").FullName;
        var secrets = new SecretStore(dir);
        return (new BlueskySource(new BlueskyClient(new HttpClient(net)), secrets), net, secrets, dir);
    }

    [Fact]
    public async Task Prepare_learns_the_DID_and_a_friend_needs_no_sign_in()
    {
        var (source, net, _, dir) = Make();
        try
        {
            var a = new Account { Network = Network.Bluesky, Handle = "@Bsky.app" };
            await source.PrepareAsync(a, default);
            Assert.Equal("did:plc:z72i7hdynmk6r22z27h6tvur", a.Id);
            Assert.Equal("bsky.app", a.Handle);
            Assert.Equal("Bluesky", a.DisplayName);

            var items = await source.FetchAsync(a, Now, default);
            Assert.Equal(4, items.Count);
            Assert.All(items, i => Assert.Equal(a.Key, i.AccountKey));
            Assert.All(net.Calls, c => Assert.StartsWith("public.api.bsky.app", c, StringComparison.Ordinal));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Rons_account_without_a_password_asks_him_to_sign_in()
    {
        var (source, _, _, dir) = Make();
        try
        {
            var mine = new Account { Network = Network.Bluesky, Handle = "ron.test", Id = "did:plc:ronfixture", IsMine = true };
            var e = await Assert.ThrowsAsync<SourceException>(() => source.FetchAsync(mine, Now, default));
            Assert.Equal(SourceProblem.SignInNeeded, e.Problem);
            Assert.Contains("Bluesky sign-in", e.Message, StringComparison.Ordinal);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Rons_account_reads_notifications_on_his_PDS_and_fetches_the_posts_they_are_about()
    {
        var (source, net, secrets, dir) = Make();
        try
        {
            secrets.Save(BlueskySource.SecretName("ron.test"), "abcd-efgh-ijkl-mnop");
            var mine = new Account { Network = Network.Bluesky, Handle = "ron.test", Id = "did:plc:ronfixture", IsMine = true };
            var items = await source.FetchAsync(mine, Now, default);

            Assert.Contains("pds.example.test/xrpc/app.bsky.notification.listNotifications", net.Calls);
            Assert.Contains(items, i => i.Kind == ItemKind.Comment);
            var old = Assert.Single(items, i => i.Text == "my old post");              // fetched because activity is on it
            Assert.True(old.Seen);

            // The access token runs out: refresh and carry on, without Ron noticing.
            net.ExpireNextAuthedCall = true;
            items = await source.FetchAsync(mine, Now, default);
            Assert.Contains(items, i => i.Kind == ItemKind.Comment);
            Assert.Equal(2, net.SessionsMade);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Slow_down_and_unknown_accounts_come_back_as_words_for_Ron()
    {
        var (source, net, _, dir) = Make();
        try
        {
            net.Override = _ =>
            {
                var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("""{"error":"RateLimitExceeded"}""") };
                r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(7));
                return r;
            };
            var a = new Account { Network = Network.Bluesky, Handle = "x.test", Id = "did:x" };
            var e = await Assert.ThrowsAsync<SourceException>(() => source.FetchAsync(a, Now, default));
            Assert.Equal(SourceProblem.RateLimited, e.Problem);
            Assert.Equal(TimeSpan.FromMinutes(7), e.RetryAfter);

            net.Override = _ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":"InvalidRequest","message":"Profile not found"}""") };
            e = await Assert.ThrowsAsync<SourceException>(() => source.PrepareAsync(new Account { Handle = "nobody.test" }, default));
            Assert.Equal(SourceProblem.NotFound, e.Problem);

            net.Override = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("""{"error":"AuthenticationRequired","message":"Invalid identifier or password"}""") };
            var client = new BlueskyClient(new HttpClient(net));
            e = await Assert.ThrowsAsync<SourceException>(() => client.SignInAsync("ron.test", "wrong", default));
            Assert.Equal(SourceProblem.SignInNeeded, e.Problem);
            Assert.Contains("App passwords", e.Message, StringComparison.Ordinal);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task All_follows_pages_until_the_cursor_runs_out()
    {
        var (source, net, _, dir) = Make();
        try
        {
            var page = 0;
            net.Override = req => req.RequestUri!.AbsolutePath.EndsWith("getFollows", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(++page == 1 ? Fixture("bluesky-follows.json") : """{"follows":[],"cursor":null}"""),
                }
                : null;
            var all = await source.Client.GetAllFollowsAsync("bsky.app", default);
            Assert.Equal(5, all.Count);
            Assert.Equal(2, page);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Secrets_round_trip_and_mask()
    {
        var dir = Directory.CreateTempSubdirectory("msp-secrets").FullName;
        try
        {
            var s = new SecretStore(dir);
            s.Save("bluesky-ron.test", "abcd-efgh-ijkl-mnop");
            Assert.NotEqual("abcd-efgh-ijkl-mnop", File.ReadAllText(Path.Combine(dir, "bluesky-ron.test.dat")));  // encrypted on disk
            Assert.Equal("abcd-efgh-ijkl-mnop", s.Load("bluesky-ron.test"));
            Assert.Equal("abcd…mnop", SecretStore.Mask("abcd-efgh-ijkl-mnop"));
            s.Delete("bluesky-ron.test");
            Assert.Null(s.Load("bluesky-ron.test"));
        }
        finally { Directory.Delete(dir, true); }
    }
}
