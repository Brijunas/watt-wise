using WattWise.Application.Accounts;
using WattWise.Application.Results;

using Unit = Mediator.Unit;

namespace WattWise.Application.Tests.Accounts;

public class SignOutEverywhereCommandTests
{
    private static readonly Guid AccountId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    public async Task Ending_every_session_of_the_account_succeeds_whatever_number_it_ended(int ended)
    {
        using SessionsHost host = new();
        host.AccountSessions.EndAllResult = ended;

        Result<Unit> result = await host.Mediator.Send(new SignOutEverywhereCommand(AccountId), Token);

        Assert.Equal((true, $"EndAll:{AccountId}"), (result.IsSuccess, string.Join(", ", host.Calls)));
    }
}
