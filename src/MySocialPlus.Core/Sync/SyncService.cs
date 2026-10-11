using MySocialPlus.Core.Model;
using MySocialPlus.Core.Sources;
using MySocialPlus.Core.Store;

namespace MySocialPlus.Core.Sync;

/// <summary>What one round of checking did, for the status bar.</summary>
public sealed record SyncReport(int AccountsChecked, int NewItems, IReadOnlyList<(Account Account, string Problem)> Problems)
{
    public static SyncReport Nothing { get; } = new(0, 0, []);
}

/// <summary>
/// Reads every account that is due, a few at a time, and puts what it finds in the cache. The
/// network calls run in parallel; the cache and the tree are only touched on the caller's thread
/// (the window's), between the awaits.
/// </summary>
public sealed class SyncService
{
    private readonly SocialTree _tree;
    private readonly ItemCache _cache;
    private readonly IReadOnlyDictionary<Network, ISource> _sources;

    public SyncService(SocialTree tree, ItemCache cache, IEnumerable<ISource> sources, PollSchedule schedule)
    {
        _tree = tree;
        _cache = cache;
        _sources = sources.ToDictionary(s => s.Network);
        Schedule = schedule;
    }

    public PollSchedule Schedule { get; }

    public int MaxParallel { get; set; } = 4;

    public bool CanRead(Account a) => a.IsReadable && _sources.ContainsKey(a.Network);

    public async Task<SyncReport> RunDueAsync(DateTimeOffset now, CancellationToken ct)
    {
        var due = _tree.Accounts().Where(a => CanRead(a) && Schedule.IsDue(a.Key, now))
                       .GroupBy(a => a.Key).Select(g => g.First()).ToList();
        if (due.Count == 0) return SyncReport.Nothing;

        using var gate = new SemaphoreSlim(MaxParallel);
        var work = due.Select(async a =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var source = _sources[a.Network];
                // Never read before: learn the account's stable id first, so its items are filed under it.
                var probe = new Account { Network = a.Network, Handle = a.Handle, Id = a.Id, IsMine = a.IsMine };
                if (a.LastChecked is null) await source.PrepareAsync(probe, ct);
                var items = await source.FetchAsync(probe, now, ct);
                return (Account: a, Probe: probe, Items: items, Error: (SourceException?)null);
            }
            catch (SourceException e) { return (Account: a, Probe: a, Items: (IReadOnlyList<Item>)[], Error: e); }
            finally { gate.Release(); }
        }).ToList();

        var results = await Task.WhenAll(work);
        var added = 0;
        var problems = new List<(Account, string)>();
        foreach (var r in results)
        {
            var a = r.Account;
            if (r.Error is { } e)
            {
                a.LastError = e.Message;
                a.LastProblem = e.Problem.ToString();
                Schedule.Failed(a.Key, now, e.RetryAfter);
                problems.Add((a, e.Message));
                continue;
            }
            var firstRead = a.LastChecked is null;
            if (!ReferenceEquals(r.Probe, a))
            {
                a.Id ??= r.Probe.Id;
                a.Handle = r.Probe.Handle;
                a.DisplayName ??= r.Probe.DisplayName;
                a.AvatarUrl = r.Probe.AvatarUrl ?? a.AvatarUrl;
            }
            // Items were made with the probe's key, which is the account's key now that the id is known.
            added += _cache.Upsert(r.Items, asSeen: firstRead);
            a.LastChecked = now;
            a.LastError = null;
            a.LastProblem = null;
            Schedule.Succeeded(a.Key, now);
        }
        return new SyncReport(results.Length, added, problems);
    }
}
