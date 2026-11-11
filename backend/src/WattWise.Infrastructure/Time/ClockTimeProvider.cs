using NodaTime;

namespace WattWise.Infrastructure.Time;

/// <summary>
/// A <see cref="TimeProvider"/> that reads the NodaTime <see cref="IClock"/>, so ASP.NET Core Identity's lockout and the
/// access token's lifetime check follow the same clock as our own code (a <c>FakeClock</c> in tests). Only the
/// current time comes from the clock; timestamps and timers are the system's.
/// </summary>
internal sealed class ClockTimeProvider(IClock clock) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        return clock.GetCurrentInstant().ToDateTimeOffset();
    }
}
