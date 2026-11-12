using System.Net;

using NodaTime;

namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>
/// The authorization middleware, not the handler, refuses a change-password call without a valid access token. The control
/// is a correctly signed token for an account that does not exist: it passes the middleware and reaches the handler (404),
/// so a 401 for the other calls can only have come before the handler. Moves the fixture's clock, so it has a fixture of its own.
/// </summary>
public class ChangePasswordAuthenticationTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed record Seeded(AccountSeeder.Ids Ids, string Password, SignedInSession Session);

    private async Task<Seeded> SeedAsync()
    {
        string email = $"user-{Guid.NewGuid():N}@example.com";
        string password = $"Aa1!{Guid.NewGuid():N}";
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        return new Seeded(ids, password, await fixture.SignInAsync(email, password));
    }

    private Task<HttpResponseMessage> ChangeAsync(string? bearer, string currentPassword) =>
        fixture.SendAsync(
            HttpMethod.Post,
            "/api/v1/auth/change-password",
            bearer: bearer,
            body: new { currentPassword, newPassword = $"Bb2!{Guid.NewGuid():N}" });

    [Fact]
    public async Task A_call_with_no_token_is_a_401_and_the_password_is_unchanged()
    {
        Seeded account = await SeedAsync();

        using HttpResponseMessage response = await ChangeAsync(null, account.Password);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "General.Unauthorized", Token);
        Assert.True(await fixture.PasswordMatchesAsync(account.Ids.IdentityId, account.Password));
    }

    [Fact]
    public async Task A_call_with_an_expired_token_is_a_401_and_the_password_is_unchanged()
    {
        Seeded account = await SeedAsync();
        fixture.Clock.Advance(Duration.FromMinutes(15));

        using HttpResponseMessage response = await ChangeAsync(account.Session.AccessToken, account.Password);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "General.Unauthorized", Token);
        Assert.True(await fixture.PasswordMatchesAsync(account.Ids.IdentityId, account.Password));
    }

    [Fact]
    public async Task A_call_with_a_tampered_token_is_a_401_and_the_password_is_unchanged()
    {
        Seeded account = await SeedAsync();
        string[] parts = account.Session.AccessToken.Split('.');
        // The first signature character carries six full bits, so changing it changes the signature bytes.
        parts[2] = (parts[2][0] == 'A' ? 'B' : 'A') + parts[2][1..];

        using HttpResponseMessage response = await ChangeAsync(string.Join('.', parts), account.Password);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "General.Unauthorized", Token);
        Assert.True(await fixture.PasswordMatchesAsync(account.Ids.IdentityId, account.Password));
    }

    [Fact]
    public async Task A_validly_signed_token_for_an_unknown_account_gets_past_the_middleware_to_the_handler()
    {
        string token = TestJwt.Mint(Guid.NewGuid().ToString());

        using HttpResponseMessage response = await ChangeAsync(token, "Aa1!whatever");

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.NotFound, "Accounts.AccountNotFound", Token);
    }
}
