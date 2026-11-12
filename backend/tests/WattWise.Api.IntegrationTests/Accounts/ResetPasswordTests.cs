using System.Net;

namespace WattWise.Api.IntegrationTests.Accounts;

public class ResetPasswordTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string ValidPassword() => $"Aa1!{Guid.NewGuid():N}";

    private sealed record Seeded(AccountSeeder.Ids Ids, string Email, string Password, string Token);

    private async Task<Seeded> SeedAsync()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        return new Seeded(ids, email, password, await fixture.IssuePasswordResetTokenAsync(ids.IdentityId));
    }

    private Task<HttpResponseMessage> ResetAsync(Seeded account, string newPassword) =>
        fixture.PostAsync("reset-password", new { id = account.Ids.IdentityId, token = account.Token, newPassword });

    private Task<HttpResponseMessage> SignInAsync(string email, string password) =>
        fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/sign-in", body: new { email, password });

    private Task<HttpResponseMessage> RefreshAsync(string cookie) =>
        fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", cookie: cookie);

    [Fact]
    public async Task A_reset_link_answers_204_and_sets_the_new_password()
    {
        Seeded account = await SeedAsync();
        string newPassword = ValidPassword();

        using HttpResponseMessage response = await ResetAsync(account, newPassword);

        Assert.Equal(
            (HttpStatusCode.NoContent, true),
            (response.StatusCode, await fixture.PasswordMatchesAsync(account.Ids.IdentityId, newPassword)));
    }

    [Fact]
    public async Task After_a_reset_the_new_password_signs_in_and_the_old_one_does_not()
    {
        Seeded account = await SeedAsync();
        string newPassword = ValidPassword();
        using HttpResponseMessage reset = await ResetAsync(account, newPassword);

        using HttpResponseMessage withNew = await SignInAsync(account.Email, newPassword);
        using HttpResponseMessage withOld = await SignInAsync(account.Email, account.Password);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Unauthorized), (withNew.StatusCode, withOld.StatusCode));
    }

    [Fact]
    public async Task A_second_use_of_the_link_fails_with_InvalidEmailLink_and_keeps_the_first_password()
    {
        Seeded account = await SeedAsync();
        string firstPassword = ValidPassword();
        using HttpResponseMessage first = await ResetAsync(account, firstPassword);

        using HttpResponseMessage second = await ResetAsync(account, ValidPassword());

        await ProblemDetailsAssertions.AssertProblemAsync(second, HttpStatusCode.BadRequest, "Accounts.InvalidEmailLink", Token);
        Assert.True(await fixture.PasswordMatchesAsync(account.Ids.IdentityId, firstPassword));
    }

    [Fact]
    public async Task An_altered_token_fails_with_InvalidEmailLink_and_changes_nothing()
    {
        Seeded account = await SeedAsync();
        string altered = (account.Token[0] == 'A' ? 'B' : 'A') + account.Token[1..];

        using HttpResponseMessage response = await fixture.PostAsync(
            "reset-password", new { id = account.Ids.IdentityId, token = altered, newPassword = ValidPassword() });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Accounts.InvalidEmailLink", Token);
        Assert.True(await fixture.PasswordMatchesAsync(account.Ids.IdentityId, account.Password));
    }

    [Fact]
    public async Task An_id_of_no_account_fails_with_InvalidEmailLink()
    {
        Seeded account = await SeedAsync();

        using HttpResponseMessage response = await fixture.PostAsync(
            "reset-password", new { id = Guid.NewGuid(), token = account.Token, newPassword = ValidPassword() });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Accounts.InvalidEmailLink", Token);
    }

    [Fact]
    public async Task An_id_that_is_not_a_guid_answers_400_with_an_id_field_error()
    {
        using HttpResponseMessage response = await fixture.PostAsync(
            "reset-password", new { id = "not-a-guid", token = "token", newPassword = ValidPassword() });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Validation.Failed", Token);
        Assert.Equal(["id"], await AuthProblems.ErrorFieldsAsync(response));
    }

    [Fact]
    public async Task A_rule_breaking_new_password_answers_400_with_a_new_password_field_error_and_leaves_the_link_usable()
    {
        Seeded account = await SeedAsync();

        using HttpResponseMessage rejected = await ResetAsync(account, "short");
        using HttpResponseMessage retry = await ResetAsync(account, ValidPassword());

        await ProblemDetailsAssertions.AssertProblemAsync(rejected, HttpStatusCode.BadRequest, "Validation.Failed", Token);
        Assert.Equal(
            ("newpassword", HttpStatusCode.NoContent),
            (string.Join(',', await AuthProblems.ErrorFieldsAsync(rejected)), retry.StatusCode));
    }

    [Fact]
    public async Task A_reset_rejects_every_refresh_token_issued_before_it()
    {
        Seeded account = await SeedAsync();
        SignedInSession first = await fixture.SignInAsync(account.Email, account.Password);
        SignedInSession second = await fixture.SignInAsync(account.Email, account.Password);
        using HttpResponseMessage reset = await ResetAsync(account, ValidPassword());

        using HttpResponseMessage firstRefresh = await RefreshAsync(first.Cookie);
        using HttpResponseMessage secondRefresh = await RefreshAsync(second.Cookie);

        Assert.Equal(
            (HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, "0"),
            (firstRefresh.StatusCode, secondRefresh.StatusCode, await fixture.SessionCountAsync(account.Ids.AccountId)));
    }

    [Fact]
    public async Task A_refused_reset_leaves_the_accounts_sessions_alone()
    {
        Seeded account = await SeedAsync();
        SignedInSession session = await fixture.SignInAsync(account.Email, account.Password);

        using HttpResponseMessage rejected = await fixture.PostAsync(
            "reset-password", new { id = account.Ids.IdentityId, token = "bad-token", newPassword = ValidPassword() });

        using HttpResponseMessage refresh = await RefreshAsync(session.Cookie);
        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.OK), (rejected.StatusCode, refresh.StatusCode));
    }

    [Fact]
    public async Task A_session_whose_security_stamp_was_replaced_without_ending_it_fails_refresh_and_is_deleted()
    {
        Seeded account = await SeedAsync();
        SignedInSession session = await fixture.SignInAsync(account.Email, account.Password);
        await fixture.RotateSecurityStampAsync(account.Ids.IdentityId);

        using HttpResponseMessage refresh = await RefreshAsync(session.Cookie);

        await ProblemDetailsAssertions.AssertProblemAsync(refresh, HttpStatusCode.Unauthorized, "Accounts.InvalidRefreshToken", Token);
        Assert.Equal("0", await fixture.SessionCountAsync(account.Ids.AccountId));
    }

    [Fact]
    public async Task A_reset_lifts_a_lockout_so_the_new_password_signs_in_at_once()
    {
        Seeded account = await SeedAsync();
        for (int attempt = 0; attempt < 5; attempt++)
        {
            using HttpResponseMessage failed = await SignInAsync(account.Email, ValidPassword());
        }

        using HttpResponseMessage locked = await SignInAsync(account.Email, account.Password);
        Assert.Equal(HttpStatusCode.Locked, locked.StatusCode);
        string newPassword = ValidPassword();
        using HttpResponseMessage reset = await ResetAsync(account, newPassword);

        using HttpResponseMessage signedIn = await SignInAsync(account.Email, newPassword);

        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
    }
}
