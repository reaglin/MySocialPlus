using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace MySocialPlus.Core.Sources.Bluesky;

/// <summary>
/// Talks to Bluesky over its official XRPC API. Public reads (anyone's posts, profiles, follow lists)
/// go to the public AppView and need no sign-in. Ron's notifications — the replies and likes on his
/// posts — need a session, made from an <b>app password</b> (never his real password).
/// </summary>
public sealed class BlueskyClient
{
    public const string PublicHost = "https://public.api.bsky.app";
    public const string Entryway = "https://bsky.social";

    private readonly HttpClient _http;
    private readonly Dictionary<string, Session> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public BlueskyClient(HttpClient http) => _http = http;

    private sealed record Session(string Did, string Handle, string AccessJwt, string RefreshJwt, string Host);

    // ---- public reads ----------------------------------------------------------------------

    public async Task<BlueskyActor> GetProfileAsync(string actor, CancellationToken ct)
    {
        var root = await GetAsync(PublicHost, "app.bsky.actor.getProfile", $"actor={Uri.EscapeDataString(Clean(actor))}", null, ct);
        return BlueskyMapper.Actor(root) ?? throw new SourceException(SourceProblem.NotFound,
            $"Bluesky has no account called \"{actor}\". Check the spelling — a handle looks like name.bsky.social.");
    }

    public Task<JsonNode?> GetAuthorFeedAsync(string actor, int limit, CancellationToken ct) =>
        GetAsync(PublicHost, "app.bsky.feed.getAuthorFeed",
                 $"actor={Uri.EscapeDataString(Clean(actor))}&limit={limit}&filter=posts_no_replies", null, ct);

    public async Task<JsonNode?> GetPostsAsync(IReadOnlyList<string> uris, CancellationToken ct)
    {
        var q = string.Join("&", uris.Take(25).Select(u => "uris=" + Uri.EscapeDataString(u)));
        return await GetAsync(PublicHost, "app.bsky.feed.getPosts", q, null, ct);
    }

    /// <summary>Everyone the account follows, page by page (Bluesky gives at most 100 a page).</summary>
    public async Task<List<BlueskyActor>> GetAllFollowsAsync(string actor, CancellationToken ct, int max = 5000)
    {
        var all = new List<BlueskyActor>();
        string? cursor = null;
        do
        {
            var q = $"actor={Uri.EscapeDataString(Clean(actor))}&limit=100" + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor));
            var (people, next) = BlueskyMapper.FromFollows(await GetAsync(PublicHost, "app.bsky.graph.getFollows", q, null, ct));
            all.AddRange(people);
            cursor = people.Count == 0 ? null : next;
        } while (cursor is not null && all.Count < max);
        return all;
    }

    // ---- signed-in reads -------------------------------------------------------------------

    /// <summary>
    /// Checks a handle and app password by signing in. Returns the account's DID and handle;
    /// throws <see cref="SourceException"/> with <see cref="SourceProblem.SignInNeeded"/> when Bluesky says no.
    /// </summary>
    public async Task<BlueskyActor> SignInAsync(string handle, string appPassword, CancellationToken ct)
    {
        var s = await CreateSessionAsync(Clean(handle), appPassword, ct);
        _sessions[Clean(handle)] = s;
        return new BlueskyActor(s.Did, s.Handle, null, null, null);
    }

    public void SignOut(string handle) => _sessions.Remove(Clean(handle));

    /// <summary>The newest notifications for a signed-in account.</summary>
    public Task<JsonNode?> ListNotificationsAsync(string handle, string appPassword, int limit, CancellationToken ct) =>
        AuthedGetAsync(handle, appPassword, "app.bsky.notification.listNotifications", $"limit={limit}", ct);

    private async Task<JsonNode?> AuthedGetAsync(string handle, string appPassword, string nsid, string query, CancellationToken ct)
    {
        handle = Clean(handle);
        if (!_sessions.TryGetValue(handle, out var s))
            _sessions[handle] = s = await CreateSessionAsync(handle, appPassword, ct);
        try
        {
            return await GetAsync(s.Host, nsid, query, s.AccessJwt, ct);
        }
        catch (SourceException e) when (e.Problem == SourceProblem.SignInNeeded)
        {
            // The access token lasts a couple of hours: refresh it, or sign in again with the app password.
            s = await RefreshAsync(s, ct) ?? await CreateSessionAsync(handle, appPassword, ct);
            _sessions[handle] = s;
            return await GetAsync(s.Host, nsid, query, s.AccessJwt, ct);
        }
    }

    private async Task<Session> CreateSessionAsync(string handle, string appPassword, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{Entryway}/xrpc/com.atproto.server.createSession")
        {
            Content = JsonContent.Create(new { identifier = handle, password = appPassword }),
        };
        var root = await SendAsync(req, ct, signingIn: true);
        return ToSession(root) ?? throw new SourceException(SourceProblem.Failed, "Bluesky's answer to signing in made no sense. Try again later.");
    }

    private async Task<Session?> RefreshAsync(Session s, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{Entryway}/xrpc/com.atproto.server.refreshSession");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.RefreshJwt);
        try { return ToSession(await SendAsync(req, ct, signingIn: true)); }
        catch (SourceException e) when (e.Problem == SourceProblem.SignInNeeded) { return null; }
    }

    private static Session? ToSession(JsonNode? root)
    {
        string? S(JsonNode? n, string k) => n?[k] is JsonValue v && v.TryGetValue<string>(out var x) ? x : null;
        var did = S(root, "did");
        var access = S(root, "accessJwt");
        var refresh = S(root, "refreshJwt");
        if (did is null || access is null || refresh is null) return null;
        // Signed-in calls go to the account's own server (its PDS), which passes app.bsky.* on to Bluesky.
        var host = Entryway;
        if (root?["didDoc"]?["service"] is JsonArray services)
            foreach (var svc in services)
                if (S(svc, "type") == "AtprotoPersonalDataServer" && S(svc, "serviceEndpoint") is { } ep) host = ep.TrimEnd('/');
        return new Session(did, S(root, "handle") ?? "", access, refresh, host);
    }

    // ---- plumbing --------------------------------------------------------------------------

    private async Task<JsonNode?> GetAsync(string host, string nsid, string query, string? token, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{host}/xrpc/{nsid}?{query}");
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await SendAsync(req, ct, signingIn: false);
    }

    private async Task<JsonNode?> SendAsync(HttpRequestMessage req, CancellationToken ct, bool signingIn)
    {
        HttpResponseMessage resp;
        try { resp = await _http.SendAsync(req, ct); }
        catch (HttpRequestException e)
        {
            throw new SourceException(SourceProblem.Failed, "Could not reach Bluesky. Check the internet connection; MySocial+ will try again.", inner: e);
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new SourceException(SourceProblem.Failed, "Bluesky took too long to answer. MySocial+ will try again.", inner: e);
        }

        using (resp)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            JsonNode? json = null;
            try { json = body.Length == 0 ? null : JsonNode.Parse(body); } catch (System.Text.Json.JsonException) { }
            if (resp.IsSuccessStatusCode) return json;

            var error = json?["error"]?.GetValue<string>() ?? "";
            var message = json?["message"]?.GetValue<string>() ?? resp.ReasonPhrase ?? "";
            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                throw new SourceException(SourceProblem.RateLimited,
                    "Bluesky asked MySocial+ to slow down. It will wait and try again by itself.", RetryAfter(resp));
            if (signingIn && resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
                throw new SourceException(SourceProblem.SignInNeeded,
                    "Bluesky did not accept that handle and app password. Check the handle (like name.bsky.social) and " +
                    "make a new app password in Bluesky: Settings ▸ Privacy and security ▸ App passwords.");
            if (resp.StatusCode == HttpStatusCode.Unauthorized || error is "ExpiredToken" or "InvalidToken" or "AuthenticationRequired")
                throw new SourceException(SourceProblem.SignInNeeded, "Bluesky's sign-in has run out. MySocial+ will sign in again.");
            if (error is "ActorNotFound" or "NotFound" || message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                throw new SourceException(SourceProblem.NotFound,
                    "Bluesky could not find this account. It may have been deleted, or its handle changed.");
            throw new SourceException(SourceProblem.Failed, $"Bluesky answered with an error ({(int)resp.StatusCode} {error}: {message}). MySocial+ will try again.");
        }
    }

    /// <summary>Bluesky says when the limit resets as Unix seconds in <c>ratelimit-reset</c>.</summary>
    private static TimeSpan? RetryAfter(HttpResponseMessage resp)
    {
        if (resp.Headers.RetryAfter?.Delta is { } d) return d;
        if (resp.Headers.TryGetValues("ratelimit-reset", out var v) && long.TryParse(v.FirstOrDefault(), out var unix))
        {
            var wait = DateTimeOffset.FromUnixTimeSeconds(unix) - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : null;
        }
        return null;
    }

    /// <summary>"@Name.bsky.social " → "name.bsky.social".</summary>
    public static string Clean(string handle) => handle.Trim().TrimStart('@').ToLowerInvariant();
}
