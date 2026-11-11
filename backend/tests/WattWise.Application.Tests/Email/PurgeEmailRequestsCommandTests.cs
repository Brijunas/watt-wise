using WattWise.Application.Email;

namespace WattWise.Application.Tests.Email;

public class PurgeEmailRequestsCommandTests : IDisposable
{
    private readonly EmailHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task Requests_created_more_than_30_days_ago_are_purged()
    {
        await _host.Mediator.Send(new PurgeEmailRequestsCommand(), TestContext.Current.CancellationToken);

        // 2026-11-11T12:00Z minus 30 days.
        Assert.Equal(NodaTime.Instant.FromUtc(2026, 10, 12, 12, 0), _host.Outbox.PurgedBefore);
    }

    [Fact]
    public async Task The_response_carries_the_number_of_rows_deleted()
    {
        _host.Outbox.PurgeResult = 7;

        var result = await _host.Mediator.Send(new PurgeEmailRequestsCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(new PurgeEmailRequestsResponse(7), result.Value);
    }
}
