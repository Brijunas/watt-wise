using NodaTime;

using WattWise.Application.Accounts;
using WattWise.Application.Results;

namespace WattWise.Application.Tests.Accounts;

public sealed class AccountErrorsTests
{
    [Fact]
    public void Lockout_keeps_the_retry_duration_with_the_documented_error()
    {
        Error error = AccountErrors.LockedOut(Duration.FromMilliseconds(1234));

        Assert.Equal(
            new Error("Accounts.LockedOut", "Too many failed sign-ins; try again later.", ErrorType.Locked, RetryAfter: Duration.FromMilliseconds(1234)),
            error);
    }
}
