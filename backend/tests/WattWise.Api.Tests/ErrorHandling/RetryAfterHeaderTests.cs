using Microsoft.AspNetCore.Http;

using NodaTime;

using WattWise.Api.ErrorHandling;
using WattWise.Application.Results;

namespace WattWise.Api.Tests.ErrorHandling;

public sealed class RetryAfterHeaderTests
{
    [Theory]
    [InlineData(900000, "900")]
    [InlineData(1201, "2")]
    [InlineData(0, "1")]
    [InlineData(-1500, "1")]
    public void A_retry_duration_becomes_whole_seconds_rounded_up_and_at_least_one(int milliseconds, string expected)
    {
        HttpResponse response = new DefaultHttpContext().Response;
        Result<int> result = Error.Locked("Accounts.LockedOut", "Wait.", Duration.FromMilliseconds(milliseconds));

        RetryAfterHeader.Set(response, result);

        Assert.Equal(expected, response.Headers.RetryAfter.ToString());
    }

    public static TheoryData<Result<int>> WithoutRetry => new()
    {
        Result<int>.Success(42),
        Error.Locked("Test.Locked", "Wait."),
        Error.Unauthorized("Accounts.InvalidCredentials", "Wrong password."),
    };

    [Theory]
    [MemberData(nameof(WithoutRetry))]
    public void A_result_without_a_retry_duration_adds_no_header(Result<int> result)
    {
        HttpResponse response = new DefaultHttpContext().Response;

        RetryAfterHeader.Set(response, result);

        Assert.False(response.Headers.ContainsKey("Retry-After"));
    }
}
