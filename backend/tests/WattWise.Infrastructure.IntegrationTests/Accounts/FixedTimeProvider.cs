namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>
/// A <see cref="TimeProvider"/> that always says the same time. ASP.NET Core Identity's <c>UserManager</c> asks the
/// service provider for one when it needs the time (lockout), so a test registers this to control it.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
