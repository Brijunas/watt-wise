using WattWise.Application.Accounts;
using WattWise.Application.Results;

using Unit = Mediator.Unit;

namespace WattWise.Application.Tests.Accounts;

public class ForgotPasswordCommandTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Asking_for_a_reset_records_a_password_reset_request_keyed_by_the_normalized_address_and_saves_without_any_lookup()
    {
        using AccountsHost host = new();

        await host.Mediator.Send(new ForgotPasswordCommand("Ada@Example.com"), Token);

        Assert.Equal(["Add:PasswordReset:normalized:Ada@Example.com", "Save"], host.Calls);
    }

    [Fact]
    public async Task Asking_for_a_reset_answers_success()
    {
        using AccountsHost host = new();

        Result<Unit> result = await host.Mediator.Send(new ForgotPasswordCommand("ada@example.com"), Token);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task An_invalid_address_records_nothing()
    {
        using AccountsHost host = new();

        Result<Unit> result = await host.Mediator.Send(new ForgotPasswordCommand("not-an-email"), Token);

        Assert.Equal(("Validation.Failed", 0), (result.Error.Code, host.Calls.Count));
    }

    [Fact]
    public void The_command_text_hides_the_address()
    {
        Assert.Equal("ForgotPasswordCommand", new ForgotPasswordCommand("ada@example.com").ToString());
    }
}
