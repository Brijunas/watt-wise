using WattWise.Application.Accounts;
using WattWise.Application.Results;

using Unit = Mediator.Unit;

namespace WattWise.Application.Tests.Accounts;

public class ResendConfirmationCommandTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Resending_asks_for_a_confirmation_keyed_by_the_normalized_address_and_saves_without_any_lookup()
    {
        using AccountsHost host = new();

        Result<Unit> result = await host.Mediator.Send(new ResendConfirmationCommand("Ada@Example.com"), Token);

        Assert.Equal(["Add:EmailConfirmation:normalized:Ada@Example.com", "Save"], host.Calls);
    }

    [Fact]
    public async Task Resending_answers_success()
    {
        using AccountsHost host = new();

        Result<Unit> result = await host.Mediator.Send(new ResendConfirmationCommand("ada@example.com"), Token);

        Assert.True(result.IsSuccess);
    }
}
