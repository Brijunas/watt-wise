using WattWise.Application.Accounts;
using WattWise.Application.Results;

using Unit = Mediator.Unit;

namespace WattWise.Application.Tests.Accounts;

public class EndSessionCommandTests
{
    private static readonly Guid AccountId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid SessionId = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_session_of_the_account_is_ended_by_asking_the_port_with_both_ids()
    {
        using SessionsHost host = new();

        Result<Unit> result = await host.Mediator.Send(new EndSessionCommand(AccountId, SessionId), Token);

        Assert.Equal((true, $"End:{AccountId}:{SessionId}"), (result.IsSuccess, string.Join(", ", host.Calls)));
    }

    [Fact]
    public async Task A_session_the_port_ends_nothing_for_is_SessionNotFound()
    {
        using SessionsHost host = new();
        host.AccountSessions.EndResult = 0;

        Result<Unit> result = await host.Mediator.Send(new EndSessionCommand(AccountId, SessionId), Token);

        Assert.Equal(new Error("Accounts.SessionNotFound", "The session does not exist.", ErrorType.NotFound), result.Error);
    }
}
