using NodaTime;

using WattWise.Application.Accounts;
using WattWise.Application.Results;

namespace WattWise.Application.Tests.Accounts;

public class PurgeExpiredSessionsCommandTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_purge_runs_as_of_the_current_time()
    {
        using SessionsHost host = new();

        await host.Mediator.Send(new PurgeExpiredSessionsCommand(), Token);

        Assert.Equal(Instant.FromUtc(2026, 11, 11, 12, 0), host.ExpiredSessions.PurgedAt);
    }

    [Fact]
    public async Task The_response_carries_the_number_of_sessions_deleted()
    {
        using SessionsHost host = new();
        host.ExpiredSessions.Result = 7;

        Result<PurgeExpiredSessionsResponse> result = await host.Mediator.Send(new PurgeExpiredSessionsCommand(), Token);

        Assert.Equal(new PurgeExpiredSessionsResponse(7), result.Value);
    }
}
