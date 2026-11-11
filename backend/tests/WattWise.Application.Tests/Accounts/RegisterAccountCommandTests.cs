using WattWise.Application.Accounts;
using WattWise.Application.Results;

using Unit = Mediator.Unit;

namespace WattWise.Application.Tests.Accounts;

public class RegisterAccountCommandTests
{
    private static readonly Guid ExistingId = Guid.Parse("00000000-0000-0000-0000-0000000000e1");
    private static readonly Guid NewId = Guid.Parse("00000000-0000-0000-0000-0000000000a2");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_taken_address_hashes_and_drops_the_password_asks_for_the_already_exists_email_and_saves_once()
    {
        using AccountsHost host = new();
        host.Registration.Existing = ExistingId;

        Result<Unit> result = await host.Mediator.Send(new RegisterAccountCommand("Ada@Example.com", "Aa1!secret", "lt"), Token);

        Assert.Equal(
            ["Check:Aa1!secret", "Find:normalized:Ada@Example.com", "HashAndDiscard:Aa1!secret", $"Add:AccountAlreadyExists:{ExistingId}", "Save"],
            host.Calls);
    }

    [Fact]
    public async Task A_taken_address_answers_success()
    {
        using AccountsHost host = new();
        host.Registration.Existing = ExistingId;

        Result<Unit> result = await host.Mediator.Send(new RegisterAccountCommand("ada@example.com", "Aa1!secret", "lt"), Token);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("lt", "Lt")]
    [InlineData("en", "En")]
    public async Task A_new_address_stages_an_account_in_the_language_and_creates_its_identity_with_the_confirmation_request(string language, string staged)
    {
        using AccountsHost host = new();

        Result<Unit> result = await host.Mediator.Send(new RegisterAccountCommand("ada@example.com", "Aa1!secret", language), Token);

        Assert.Equal(
            [
                "Check:Aa1!secret",
                "Find:normalized:ada@example.com",
                $"Stage:{staged}",
                $"Add:EmailConfirmation:{NewId}",
                $"CreateIdentity:{NewId}:normalized:ada@example.com:Aa1!secret",
            ],
            host.Calls);
    }

    [Fact]
    public async Task A_new_address_answers_success()
    {
        using AccountsHost host = new();

        Result<Unit> result = await host.Mediator.Send(new RegisterAccountCommand("ada@example.com", "Aa1!secret", "en"), Token);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task An_address_taken_in_the_meantime_still_answers_success()
    {
        using AccountsHost host = new();
        host.Registration.Created = false;

        Result<Unit> result = await host.Mediator.Send(new RegisterAccountCommand("ada@example.com", "Aa1!secret", "en"), Token);

        Assert.True(result.IsSuccess);
    }
}
