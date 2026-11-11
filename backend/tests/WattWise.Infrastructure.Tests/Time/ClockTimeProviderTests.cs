using NodaTime;
using NodaTime.Testing;

using WattWise.Infrastructure.Time;

namespace WattWise.Infrastructure.Tests.Time;

public class ClockTimeProviderTests
{
    [Fact]
    public void The_time_is_the_clocks_current_instant()
    {
        FakeClock clock = new(Instant.FromUtc(2026, 11, 11, 12, 0));

        Assert.Equal(new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero), new ClockTimeProvider(clock).GetUtcNow());
    }

    [Fact]
    public void The_time_follows_the_clock_when_it_moves()
    {
        FakeClock clock = new(Instant.FromUtc(2026, 11, 11, 12, 0));
        ClockTimeProvider provider = new(clock);

        clock.Advance(Duration.FromMinutes(15));

        Assert.Equal(new DateTimeOffset(2026, 11, 11, 12, 15, 0, TimeSpan.Zero), provider.GetUtcNow());
    }
}
