using WattWise.Application.Accounts;
using WattWise.Application.Results;

namespace WattWise.Application.Tests.Accounts;

public class GetMeQueryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_known_account_answers_what_the_query_port_returns()
    {
        using SessionsHost host = new();
        Me me = new("ada@example.com", "lt", ["admin"], ["a.read", "b.write"]);
        host.Queries.Me = me;

        Result<Me> result = await host.Mediator.Send(new GetMeQuery(Guid.NewGuid()), Token);

        Assert.Same(me, result.Value);
    }

    [Fact]
    public async Task An_unknown_account_is_AccountNotFound()
    {
        using SessionsHost host = new();

        Result<Me> result = await host.Mediator.Send(new GetMeQuery(Guid.NewGuid()), Token);

        Assert.Equal(new Error("Accounts.AccountNotFound", "The account does not exist.", ErrorType.NotFound), result.Error);
    }
}
