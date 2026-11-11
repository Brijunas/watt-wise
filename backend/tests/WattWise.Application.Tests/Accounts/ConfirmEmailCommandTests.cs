using WattWise.Application.Accounts;
using WattWise.Application.Results;

using Unit = Mediator.Unit;

namespace WattWise.Application.Tests.Accounts;

public class ConfirmEmailCommandTests
{
    private const string Id = "00000000-0000-0000-0000-0000000000c3";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_valid_link_confirms_the_address_for_the_parsed_id()
    {
        using AccountsHost host = new();

        Result<Unit> result = await host.Mediator.Send(new ConfirmEmailCommand(Id, "tok"), Token);

        Assert.Equal([$"Confirm:{Id}:tok"], host.Calls);
    }

    [Fact]
    public async Task A_valid_link_succeeds()
    {
        using AccountsHost host = new();

        Result<Unit> result = await host.Mediator.Send(new ConfirmEmailCommand(Id, "tok"), Token);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task A_bad_link_fails_with_the_InvalidEmailLink_bad_request()
    {
        using AccountsHost host = new();
        host.Confirmation.Valid = false;

        Result<Unit> result = await host.Mediator.Send(new ConfirmEmailCommand(Id, "tok"), Token);

        Assert.Equal(
            new Error("Accounts.InvalidEmailLink", "The link is invalid or has expired.", ErrorType.BadRequest),
            result.Error);
    }
}
