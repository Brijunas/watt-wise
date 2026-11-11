using NodaTime;

using WattWise.Application.Accounts;
using WattWise.Application.Results;
using WattWise.Domain.Accounts;

namespace WattWise.Application.Tests.Accounts;

public class RefreshSessionCommandTests
{
    private static readonly Guid AccountId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // token-1 is the one the sign-in issued; the handler's next one is token-2.
    private static async Task<SessionsHost> SignedInHostAsync()
    {
        SessionsHost host = new(PasswordCheck.Succeeded(AccountId));
        await host.Mediator.Send(new SignInCommand("ada@example.com", "pw", "agent"), Token);
        host.Calls.Clear();
        return host;
    }

    [Fact]
    public async Task A_valid_token_is_rotated_and_answers_new_tokens_valid_from_now()
    {
        using SessionsHost host = await SignedInHostAsync();
        host.Clock.Advance(Duration.FromMinutes(20));

        Result<SignedIn> result = await host.Mediator.Send(new RefreshSessionCommand("token-1"), Token);

        // The sign-in issued token-1, so the handler's new token is token-2; 12:20 plus 15 minutes and plus 7 days.
        Assert.Equal(
            new SignedIn("jwt", Instant.FromUtc(2026, 11, 11, 12, 35), "token-2", Instant.FromUtc(2026, 11, 18, 12, 20)),
            result.Value);
    }

    [Fact]
    public async Task A_rotation_stores_the_new_hash_marks_the_old_token_used_and_saves_before_issuing()
    {
        using SessionsHost host = await SignedInHostAsync();
        host.Queries.Permissions = ["a.read"];
        host.Clock.Advance(Duration.FromMinutes(20));

        await host.Mediator.Send(new RefreshSessionCommand("token-1"), Token);

        Session session = host.Sessions.Stored.Single();
        Assert.Equal(
            $"Save, Issue:{AccountId}:a.read:2026-11-11T12:20:00Z / 746F6B656E2D31|2026-11-11T12:20:00Z, 746F6B656E2D32|",
            $"{string.Join(", ", host.Calls)} / "
                + string.Join(", ", session.RefreshTokens.Select(token => $"{Convert.ToHexString(token.TokenHash)}|{token.UsedAt}")));
    }

    [Fact]
    public async Task The_new_access_token_carries_the_permissions_read_at_refresh_time()
    {
        using SessionsHost host = await SignedInHostAsync();
        host.Queries.Permissions = ["new.permission"];

        await host.Mediator.Send(new RefreshSessionCommand("token-1"), Token);

        Assert.Equal($"Issue:{AccountId}:new.permission:2026-11-11T12:00:00Z", host.Calls[^1]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_missing_token_is_refused_without_touching_the_session(string? token)
    {
        using SessionsHost host = await SignedInHostAsync();

        Result<SignedIn> result = await host.Mediator.Send(new RefreshSessionCommand(token), Token);

        Assert.Equal(("Accounts.InvalidRefreshToken", ErrorType.Unauthorized, 1, 0), (result.Error.Code, result.Error.Type, host.Sessions.Stored.Count, host.Calls.Count));
    }

    [Fact]
    public async Task An_unknown_token_is_refused_without_touching_any_session()
    {
        using SessionsHost host = await SignedInHostAsync();

        Result<SignedIn> result = await host.Mediator.Send(new RefreshSessionCommand("other"), Token);

        Assert.Equal(
            (new Error("Accounts.InvalidRefreshToken", "The refresh token is missing, invalid or expired.", ErrorType.Unauthorized), 1, 0),
            (result.Error, host.Sessions.Stored.Count, host.Calls.Count));
    }

    [Fact]
    public async Task An_expired_token_is_refused_and_the_session_stays()
    {
        using SessionsHost host = await SignedInHostAsync();
        host.Clock.Advance(Duration.FromDays(7));

        Result<SignedIn> result = await host.Mediator.Send(new RefreshSessionCommand("token-1"), Token);

        Assert.Equal(("Accounts.InvalidRefreshToken", 1, 0), (result.Error.Code, host.Sessions.Stored.Count, host.Calls.Count));
    }

    [Fact]
    public async Task A_token_one_second_before_its_expiry_still_refreshes()
    {
        using SessionsHost host = await SignedInHostAsync();
        host.Clock.Advance(Duration.FromDays(7) - Duration.FromSeconds(1));

        Result<SignedIn> result = await host.Mediator.Send(new RefreshSessionCommand("token-1"), Token);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task A_reused_token_removes_the_session_saves_and_is_refused()
    {
        using SessionsHost host = await SignedInHostAsync();
        await host.Mediator.Send(new RefreshSessionCommand("token-1"), Token);
        host.Calls.Clear();

        Result<SignedIn> result = await host.Mediator.Send(new RefreshSessionCommand("token-1"), Token);

        Assert.Equal(("Accounts.InvalidRefreshToken", "RemoveSession, Save", 0), (result.Error.Code, string.Join(", ", host.Calls), host.Sessions.Stored.Count));
    }

    [Fact]
    public void The_command_text_hides_the_token()
    {
        Assert.Equal("RefreshSessionCommand", new RefreshSessionCommand("secret").ToString());
    }
}
