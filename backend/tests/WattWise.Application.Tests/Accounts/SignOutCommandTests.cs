using WattWise.Application.Accounts;
using WattWise.Application.Results;

using Unit = Mediator.Unit;

namespace WattWise.Application.Tests.Accounts;

public class SignOutCommandTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<SessionsHost> SignedInHostAsync()
    {
        SessionsHost host = new(PasswordCheck.Succeeded(Guid.Parse("00000000-0000-0000-0000-0000000000a1"), "stamp"));
        await host.Mediator.Send(new SignInCommand("ada@example.com", "pw", "agent"), Token);
        host.Calls.Clear();
        return host;
    }

    [Fact]
    public async Task A_known_token_removes_its_session_and_saves()
    {
        using SessionsHost host = await SignedInHostAsync();

        Result<Unit> result = await host.Mediator.Send(new SignOutCommand("token-1"), Token);

        Assert.Equal((true, "RemoveSession, Save", 0), (result.IsSuccess, string.Join(", ", host.Calls), host.Sessions.Stored.Count));
    }

    [Fact]
    public async Task A_token_that_was_already_exchanged_still_ends_its_session()
    {
        using SessionsHost host = await SignedInHostAsync();
        await host.Mediator.Send(new RefreshSessionCommand("token-1"), Token);

        await host.Mediator.Send(new SignOutCommand("token-1"), Token);

        Assert.Empty(host.Sessions.Stored);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    public async Task A_missing_or_unknown_token_succeeds_and_changes_nothing(string? token)
    {
        using SessionsHost host = await SignedInHostAsync();

        Result<Unit> result = await host.Mediator.Send(new SignOutCommand(token), Token);

        Assert.Equal((true, 0, 1), (result.IsSuccess, host.Calls.Count, host.Sessions.Stored.Count));
    }

    [Fact]
    public void The_command_text_hides_the_token()
    {
        Assert.Equal("SignOutCommand", new SignOutCommand("secret").ToString());
    }
}
