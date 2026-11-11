using System.Net;

namespace WattWise.Api.IntegrationTests.Accounts;

public class SignInErrorTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string ValidPassword() => $"Aa1!{Guid.NewGuid():N}";

    private Task<HttpResponseMessage> SignInAsync(string email, string password) =>
        fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/sign-in", body: new { email, password });

    [Fact]
    public async Task A_wrong_password_answers_401_InvalidCredentials()
    {
        string email = UniqueEmail();
        await fixture.CreateAccountAsync(email, ValidPassword());

        using HttpResponseMessage response = await SignInAsync(email, ValidPassword());

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Accounts.InvalidCredentials", Token);
    }

    [Fact]
    public async Task An_unknown_email_answers_the_same_401_so_accounts_cannot_be_told_apart()
    {
        string email = UniqueEmail();
        await fixture.CreateAccountAsync(email, ValidPassword());

        using HttpResponseMessage wrongPassword = await SignInAsync(email, ValidPassword());
        using HttpResponseMessage unknownEmail = await SignInAsync(UniqueEmail(), ValidPassword());

        Assert.Equal(
            await AuthProblems.BodyWithoutTraceIdAsync(wrongPassword),
            await AuthProblems.BodyWithoutTraceIdAsync(unknownEmail));
    }

    [Fact]
    public async Task An_unconfirmed_email_with_the_right_password_answers_403_EmailNotConfirmed()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        await fixture.CreateAccountAsync(email, password, confirmed: false);

        using HttpResponseMessage response = await SignInAsync(email, password);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Forbidden, "Accounts.EmailNotConfirmed", Token);
    }

    [Fact]
    public async Task An_unconfirmed_email_with_a_wrong_password_answers_401_InvalidCredentials_and_so_is_not_revealed()
    {
        string email = UniqueEmail();
        await fixture.CreateAccountAsync(email, ValidPassword(), confirmed: false);

        using HttpResponseMessage response = await SignInAsync(email, ValidPassword());

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Accounts.InvalidCredentials", Token);
    }

    [Fact]
    public async Task A_failed_sign_in_sets_no_cookie_and_starts_no_session()
    {
        string email = UniqueEmail();
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, ValidPassword());

        using HttpResponseMessage response = await SignInAsync(email, ValidPassword());

        Assert.Equal(
            (false, 0),
            (response.Headers.Contains("Set-Cookie"), (await fixture.SessionRowsAsync(ids.AccountId)).Length));
    }
}
