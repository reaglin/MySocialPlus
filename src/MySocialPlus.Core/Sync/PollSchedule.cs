namespace MySocialPlus.Core.Sync;

/// <summary>
/// When each account is next read. Every account on the same interval while the program is open
/// (open question 4 proposes 15 minutes); after a failure or a "slow down" from the network it
/// waits twice as long each time, up to <see cref="MaxBackoff"/>, and goes back to normal on the
/// next success. Pure arithmetic on times it is given, so tests need no clock.
/// </summary>
public sealed class PollSchedule
{
    private readonly Dictionary<string, (DateTimeOffset Next, int Failures)> _state = new(StringComparer.Ordinal);

    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromHours(2);

    /// <summary>An account never read is due at once.</summary>
    public bool IsDue(string key, DateTimeOffset now) => !_state.TryGetValue(key, out var s) || s.Next <= now;

    public DateTimeOffset? NextFor(string key) => _state.TryGetValue(key, out var s) ? s.Next : null;

    public int FailuresFor(string key) => _state.TryGetValue(key, out var s) ? s.Failures : 0;

    public void Succeeded(string key, DateTimeOffset now) => _state[key] = (now + Interval, 0);

    /// <summary>A failure or a rate limit: wait longer each time; never sooner than the network asked.</summary>
    public void Failed(string key, DateTimeOffset now, TimeSpan? retryAfter = null)
    {
        var failures = FailuresFor(key) + 1;
        var wait = TimeSpan.FromTicks(Math.Min(MaxBackoff.Ticks, Interval.Ticks * (1L << Math.Min(failures, 20))));
        if (retryAfter is { } ra && ra > wait) wait = ra;
        _state[key] = (now + wait, failures);
    }

    /// <summary>"Check now": everything is due, failures forgiven.</summary>
    public void MakeAllDue() => _state.Clear();
}
