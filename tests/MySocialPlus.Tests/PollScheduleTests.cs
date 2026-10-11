using MySocialPlus.Core.Sync;

namespace MySocialPlus.Tests;

public class PollScheduleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Due_at_once_then_every_interval()
    {
        var s = new PollSchedule { Interval = TimeSpan.FromMinutes(15) };
        Assert.True(s.IsDue("a", T0));
        s.Succeeded("a", T0);
        Assert.False(s.IsDue("a", T0.AddMinutes(14)));
        Assert.True(s.IsDue("a", T0.AddMinutes(15)));
    }

    [Fact]
    public void Failures_back_off_doubling_up_to_the_cap_and_success_resets()
    {
        var s = new PollSchedule { Interval = TimeSpan.FromMinutes(15), MaxBackoff = TimeSpan.FromHours(2) };
        s.Failed("a", T0);
        Assert.Equal(T0.AddMinutes(30), s.NextFor("a"));
        s.Failed("a", T0);
        Assert.Equal(T0.AddMinutes(60), s.NextFor("a"));
        for (var i = 0; i < 10; i++) s.Failed("a", T0);
        Assert.Equal(T0.AddHours(2), s.NextFor("a"));
        s.Succeeded("a", T0);
        Assert.Equal(0, s.FailuresFor("a"));
        Assert.Equal(T0.AddMinutes(15), s.NextFor("a"));
    }

    [Fact]
    public void Never_sooner_than_the_network_asked()
    {
        var s = new PollSchedule { Interval = TimeSpan.FromMinutes(15) };
        s.Failed("a", T0, retryAfter: TimeSpan.FromHours(3));
        Assert.Equal(T0.AddHours(3), s.NextFor("a"));
    }

    [Fact]
    public void Check_now_makes_everything_due()
    {
        var s = new PollSchedule();
        s.Failed("a", T0);
        s.Succeeded("b", T0);
        s.MakeAllDue();
        Assert.True(s.IsDue("a", T0));
        Assert.True(s.IsDue("b", T0));
    }
}
