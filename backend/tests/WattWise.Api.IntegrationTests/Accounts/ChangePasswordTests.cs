using System.Net;
using System.Text.Json;

namespace WattWise.Api.IntegrationTests.Accounts;

public class ChangePasswordTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string ValidPassword() => $"Aa1!{Guid.NewGuid():N}";

    private sealed record Seeded(AccountSeeder.Ids Ids, string Email, string Password, SignedInSession Session);

    private async Task<Seeded> SeedAsync()
    {
        string email = $"user-{Guid.NewGuid():N}@example.com";
        string password = ValidPassword();
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        return new Seeded(ids, email, password, await fixture.SignInAsync(email, password));
    }

    private Task<HttpResponseMessage> ChangeAsync(string? bearer, string? currentPassword, string? newPassword) =>
        fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/change-password", bearer: bearer, body: new { currentPassword, newPassword });

    private Task<HttpResponseMessage> SignInAsync(string email, string password) =>
        fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/sign-in", body: new { email, password });

    private Task<HttpResponseMessage> RefreshAsync(string cookie) =>
        fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", cookie: cookie);

    [Fact]
    public async Task Changing_without_a_token_answers_401_and_changes_nothing()
    {
        Seeded account = await SeedAsync();

        using HttpResponseMessage response = await ChangeAsync(null, account.Password, ValidPassword());

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "General.Unauthorized", Token);
        Assert.True(await fixture.PasswordMatchesAsync(account.Ids.IdentityId, account.Password));
    }

    [Fact]
    public async Task A_change_answers_200_with_the_access_token_and_its_expiry_and_nothing_else()
    {
        Seeded account = await SeedAsync();

        using HttpResponseMessage response = await ChangeAsync(account.Session.AccessToken, account.Password, ValidPassword());

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal(
            (HttpStatusCode.OK, "accessToken,accessTokenExpiresAt"),
            (response.StatusCode, string.Join(',', body.RootElement.EnumerateObject().Select(property => property.Name))));
    }

    [Fact]
    public async Task The_new_password_signs_in_and_the_old_one_does_not()
    {
        Seeded account = await SeedAsync();
        string next = ValidPassword();
        using HttpResponseMessage change = await ChangeAsync(account.Session.AccessToken, account.Password, next);

        using HttpResponseMessage withNew = await SignInAsync(account.Email, next);
        using HttpResponseMessage withOld = await SignInAsync(account.Email, account.Password);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Unauthorized), (withNew.StatusCode, withOld.StatusCode));
    }

    [Fact]
    public async Task The_new_access_token_works()
    {
        Seeded account = await SeedAsync();
        using HttpResponseMessage change = await ChangeAsync(account.Session.AccessToken, account.Password, ValidPassword());
        using JsonDocument body = JsonDocument.Parse(await change.Content.ReadAsStringAsync(Token));

        using HttpResponseMessage me = await fixture.SendAsync(
            HttpMethod.Get, "/api/v1/me", bearer: body.RootElement.GetProperty("accessToken").GetString());

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task The_new_refresh_cookie_refreshes_and_it_is_the_only_session_left()
    {
        Seeded account = await SeedAsync();
        using HttpResponseMessage change = await ChangeAsync(account.Session.AccessToken, account.Password, ValidPassword());
        string newCookie = SetCookieHeader.RefreshCookieOf(change)!.NameAndValue;

        using HttpResponseMessage refresh = await RefreshAsync(newCookie);

        Assert.Equal((HttpStatusCode.OK, "1"), (refresh.StatusCode, await fixture.SessionCountAsync(account.Ids.AccountId)));
    }

    [Fact]
    public async Task A_change_rejects_every_refresh_token_issued_before_it()
    {
        Seeded account = await SeedAsync();
        SignedInSession other = await fixture.SignInAsync(account.Email, account.Password);
        using HttpResponseMessage change = await ChangeAsync(account.Session.AccessToken, account.Password, ValidPassword());

        using HttpResponseMessage ownRefresh = await RefreshAsync(account.Session.Cookie);
        using HttpResponseMessage otherRefresh = await RefreshAsync(other.Cookie);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (ownRefresh.StatusCode, otherRefresh.StatusCode));
    }

    [Fact]
    public async Task A_change_deletes_an_outstanding_reset_link()
    {
        Seeded account = await SeedAsync();
        string resetToken = await fixture.IssuePasswordResetTokenAsync(account.Ids.IdentityId);
        using HttpResponseMessage change = await ChangeAsync(account.Session.AccessToken, account.Password, ValidPassword());

        using HttpResponseMessage reset = await fixture.PostAsync(
            "reset-password", new { id = account.Ids.IdentityId, token = resetToken, newPassword = ValidPassword() });

        await ProblemDetailsAssertions.AssertProblemAsync(reset, HttpStatusCode.BadRequest, "Accounts.InvalidEmailLink", Token);
        Assert.Equal("0", await fixture.ResetLinkCountAsync(account.Ids.IdentityId));
    }

    [Fact]
    public async Task A_wrong_current_password_answers_400_InvalidCurrentPassword_with_a_current_password_field_error()
    {
        Seeded account = await SeedAsync();

        using HttpResponseMessage response = await ChangeAsync(account.Session.AccessToken, ValidPassword(), ValidPassword());

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Accounts.InvalidCurrentPassword", Token);
        Assert.Equal(["currentpassword"], await AuthProblems.ErrorFieldsAsync(response));
    }

    [Fact]
    public async Task A_wrong_current_password_leaves_the_password_the_session_and_the_reset_link_as_they_were()
    {
        Seeded account = await SeedAsync();
        await fixture.IssuePasswordResetTokenAsync(account.Ids.IdentityId);

        using HttpResponseMessage response = await ChangeAsync(account.Session.AccessToken, ValidPassword(), ValidPassword());

        using HttpResponseMessage refresh = await RefreshAsync(account.Session.Cookie);
        Assert.Equal(
            (true, HttpStatusCode.OK, "1"),
            (
                await fixture.PasswordMatchesAsync(account.Ids.IdentityId, account.Password),
                refresh.StatusCode,
                await fixture.ResetLinkCountAsync(account.Ids.IdentityId)));
    }

    [Fact]
    public async Task Five_wrong_current_passwords_lock_the_account_and_the_next_change_answers_423()
    {
        Seeded account = await SeedAsync();
        for (int attempt = 0; attempt < 5; attempt++)
        {
            using HttpResponseMessage wrong = await ChangeAsync(account.Session.AccessToken, ValidPassword(), ValidPassword());
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        }

        using HttpResponseMessage sixth = await ChangeAsync(account.Session.AccessToken, account.Password, ValidPassword());

        using JsonDocument body = JsonDocument.Parse(await sixth.Content.ReadAsStringAsync(Token));
        Assert.Equal(
            (423, "Accounts.LockedOut", true),
            (
                body.RootElement.GetProperty("status").GetInt32(),
                body.RootElement.GetProperty("code").GetString(),
                await fixture.PasswordMatchesAsync(account.Ids.IdentityId, account.Password)));
    }

    [Fact]
    public async Task Four_wrong_current_passwords_do_not_lock_the_account()
    {
        Seeded account = await SeedAsync();
        for (int attempt = 0; attempt < 4; attempt++)
        {
            using HttpResponseMessage wrong = await ChangeAsync(account.Session.AccessToken, ValidPassword(), ValidPassword());
        }

        using HttpResponseMessage response = await ChangeAsync(account.Session.AccessToken, account.Password, ValidPassword());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_rule_breaking_new_password_answers_400_with_a_new_password_field_error_and_changes_nothing()
    {
        Seeded account = await SeedAsync();

        using HttpResponseMessage response = await ChangeAsync(account.Session.AccessToken, account.Password, "short");

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Validation.Failed", Token);
        Assert.Equal(
            ("newpassword", true),
            (string.Join(',', await AuthProblems.ErrorFieldsAsync(response)), await fixture.PasswordMatchesAsync(account.Ids.IdentityId, account.Password)));
    }

    [Fact]
    public async Task A_missing_body_answers_400_with_both_password_fields()
    {
        Seeded account = await SeedAsync();

        using HttpResponseMessage response = await fixture.SendAsync(
            HttpMethod.Post, "/api/v1/auth/change-password", bearer: account.Session.AccessToken);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Validation.Failed", Token);
        Assert.Equal(["currentpassword", "newpassword"], await AuthProblems.ErrorFieldsAsync(response));
    }

    [Fact]
    public async Task The_new_session_is_labelled_from_the_user_agent_of_the_change()
    {
        Seeded account = await SeedAsync();
        using HttpResponseMessage response = await fixture.SendAsync(
            HttpMethod.Post,
            "/api/v1/auth/change-password",
            bearer: account.Session.AccessToken,
            body: new { currentPassword = account.Password, newPassword = ValidPassword() },
            userAgent: "Mozilla/5.0 (X11; Linux x86_64; rv:130.0) Gecko/20100101 Firefox/130.0");

        string[] rows = await fixture.SessionRowsAsync(account.Ids.AccountId);

        Assert.Equal(["Firefox on Linux|true|true|1"], rows);
    }
}
