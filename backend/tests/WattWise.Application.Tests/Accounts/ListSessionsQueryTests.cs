using NodaTime;

using WattWise.Application.Accounts;
using WattWise.Application.Results;

namespace WattWise.Application.Tests.Accounts;

public class ListSessionsQueryTests
{
    private static readonly Guid AccountId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid FirstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static void AddTwoSessions(SessionsHost host)
    {
        host.Queries.Sessions.Add(new SessionListItem(FirstId, "Firefox on Linux", Instant.FromUtc(2026, 11, 10, 8, 0), Instant.FromUtc(2026, 11, 11, 11, 0)));
        host.Queries.Sessions.Add(new SessionListItem(SecondId, "Chrome on Windows", Instant.FromUtc(2026, 11, 9, 9, 30), Instant.FromUtc(2026, 11, 9, 9, 30)));
    }

    [Fact]
    public async Task The_sessions_keep_the_ports_order_and_values_and_only_the_callers_own_is_current()
    {
        using SessionsHost host = new();
        AddTwoSessions(host);

        Result<IReadOnlyList<SessionSummary>> result = await host.Mediator.Send(new ListSessionsQuery(AccountId, SecondId), Token);

        Assert.Equal(
            [
                new SessionSummary(FirstId, "Firefox on Linux", Instant.FromUtc(2026, 11, 10, 8, 0), Instant.FromUtc(2026, 11, 11, 11, 0), false),
                new SessionSummary(SecondId, "Chrome on Windows", Instant.FromUtc(2026, 11, 9, 9, 30), Instant.FromUtc(2026, 11, 9, 9, 30), true),
            ],
            result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-0000000000ff")]
    public async Task A_missing_or_unlisted_current_session_id_marks_nothing_current(string? currentSessionId)
    {
        using SessionsHost host = new();
        AddTwoSessions(host);
        Guid? current = currentSessionId is null ? null : Guid.Parse(currentSessionId);

        Result<IReadOnlyList<SessionSummary>> result = await host.Mediator.Send(new ListSessionsQuery(AccountId, current), Token);

        Assert.Equal([false, false], result.Value.Select(session => session.IsCurrent));
    }

    [Fact]
    public async Task The_port_is_asked_for_the_account_and_the_clocks_current_instant()
    {
        using SessionsHost host = new();

        await host.Mediator.Send(new ListSessionsQuery(AccountId, null), Token);

        Assert.Equal((AccountId, Instant.FromUtc(2026, 11, 11, 12, 0)), host.Queries.SessionsRequest);
    }

    [Fact]
    public async Task An_account_with_no_live_session_gets_an_empty_list()
    {
        using SessionsHost host = new();

        Result<IReadOnlyList<SessionSummary>> result = await host.Mediator.Send(new ListSessionsQuery(AccountId, SecondId), Token);

        Assert.Empty(result.Value);
    }
}
