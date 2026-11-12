using WattWise.Application.Accounts;
using WattWise.Application.Results;

using Unit = Mediator.Unit;

namespace WattWise.Application.Tests.Accounts;

/// <summary>What the refresh and sign-out handlers do when a save loses a race with another request.</summary>
public class ConcurrentSessionEndingTests
{
    private static readonly Guid AccountId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<SessionsHost> SignedInHostAsync()
    {
        SessionsHost host = new(PasswordCheck.Succeeded(AccountId, "stamp"));
        await host.Mediator.Send(new SignInCommand("ada@example.com", "pw", "agent"), Token);
        host.Calls.Clear();
        return host;
    }

    [Fact]
    public async Task A_refresh_that_loses_the_race_is_refused_and_ends_the_session()
    {
        using SessionsHost host = await SignedInHostAsync();
        host.ConflictNextSaves(1);

        Result<SignedIn> result = await host.Mediator.Send(new RefreshSessionCommand("token-1"), Token);

        Assert.Equal(
            ("Accounts.InvalidRefreshToken", "SaveConflict, RemoveSession, Save", 0),
            (result.Error.Code, string.Join(", ", host.Calls), host.Sessions.Stored.Count));
    }

    [Fact]
    public async Task A_reused_token_still_ends_the_session_when_the_first_delete_loses_a_race()
    {
        using SessionsHost host = await SignedInHostAsync();
        await host.Mediator.Send(new RefreshSessionCommand("token-1"), Token);
        host.Calls.Clear();
        host.ConflictNextSaves(1);

        Result<SignedIn> result = await host.Mediator.Send(new RefreshSessionCommand("token-1"), Token);

        Assert.Equal(
            ("Accounts.InvalidRefreshToken", "RemoveSession, SaveConflict, RemoveSession, Save", 0),
            (result.Error.Code, string.Join(", ", host.Calls), host.Sessions.Stored.Count));
    }

    [Fact]
    public async Task A_sign_out_whose_delete_loses_a_race_loads_the_session_again_and_deletes_it()
    {
        using SessionsHost host = await SignedInHostAsync();
        host.ConflictNextSaves(1);

        Result<Unit> result = await host.Mediator.Send(new SignOutCommand("token-1"), Token);

        Assert.Equal(
            (true, "RemoveSession, SaveConflict, RemoveSession, Save", 0),
            (result.IsSuccess, string.Join(", ", host.Calls), host.Sessions.Stored.Count));
    }

    [Fact]
    public async Task A_sign_out_gives_up_after_three_lost_races_and_lets_the_conflict_out()
    {
        using SessionsHost host = await SignedInHostAsync();
        host.ConflictNextSaves(3);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(async () => await host.Mediator.Send(new SignOutCommand("token-1"), Token));

        Assert.Equal("RemoveSession, SaveConflict, RemoveSession, SaveConflict, RemoveSession, SaveConflict", string.Join(", ", host.Calls));
    }
}
