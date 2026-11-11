using System.Net;
using System.Text.Json;

namespace WattWise.Api.IntegrationTests.Accounts;

public class SignInTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private const string FirefoxOnLinux = "Mozilla/5.0 (X11; Linux x86_64; rv:130.0) Gecko/20100101 Firefox/130.0";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string ValidPassword() => $"Aa1!{Guid.NewGuid():N}";

    [Fact]
    public async Task Signing_in_answers_200_with_the_access_token_and_its_expiry_15_minutes_ahead_and_nothing_else()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        await fixture.CreateAccountAsync(email, password);

        using HttpResponseMessage response = await fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/sign-in", body: new { email, password });

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal(
            (HttpStatusCode.OK, "accessToken,accessTokenExpiresAt", "2026-11-11T12:15:00+00:00"),
            (
                response.StatusCode,
                string.Join(',', body.RootElement.EnumerateObject().Select(property => property.Name)),
                body.RootElement.GetProperty("accessTokenExpiresAt").GetString()));
    }

    [Fact]
    public async Task Signing_in_stores_one_session_with_the_device_label_and_the_sign_in_time()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);

        await fixture.SignInAsync(email, password, FirefoxOnLinux);

        Assert.Equal(["Firefox on Linux|true|true|1"], await fixture.SessionRowsAsync(ids.AccountId));
    }

    [Fact]
    public async Task A_sign_in_without_a_user_agent_is_labelled_Unknown_device()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);

        await fixture.SignInAsync(email, password);

        Assert.Equal(["Unknown device|true|true|1"], await fixture.SessionRowsAsync(ids.AccountId));
    }

    [Fact]
    public async Task Each_sign_in_starts_a_session_of_its_own()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);

        await fixture.SignInAsync(email, password);
        await fixture.SignInAsync(email, password);

        Assert.Equal(2, (await fixture.SessionRowsAsync(ids.AccountId)).Length);
    }

    [Fact]
    public async Task The_email_is_matched_in_its_normalized_form()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        await fixture.CreateAccountAsync(email, password);

        using HttpResponseMessage response = await fixture.SendAsync(
            HttpMethod.Post, "/api/v1/auth/sign-in", body: new { email = $"  {email.ToUpperInvariant()} ", password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_access_token_names_the_account_the_session_and_the_permissions_of_its_roles()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        string prefix = Guid.NewGuid().ToString("N");
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        await fixture.GrantAsync(ids.AccountId, $"{prefix}-role", $"{prefix}.read", $"{prefix}.write");

        SignedInSession session = await fixture.SignInAsync(email, password);

        Microsoft.IdentityModel.JsonWebTokens.JsonWebToken token = TestJwt.Read(session.AccessToken);
        string sessionId = await fixture.ScalarAsync($"SELECT id::text AS \"Value\" FROM app.sessions WHERE account_id = {ids.AccountId}");
        Assert.Equal(
            (ids.AccountId.ToString(), sessionId, $"{prefix}.read,{prefix}.write"),
            (token.Subject, token.GetPayloadValue<string>("sid"), string.Join(',', token.GetPayloadValue<string[]>("permission"))));
    }

    [Fact]
    public async Task A_sign_in_with_no_body_answers_the_400_validation_problem_for_both_fields()
    {
        using HttpResponseMessage response = await fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/sign-in");

        Assert.Equal(
            (HttpStatusCode.BadRequest, """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"email":["\u0027Email\u0027 must not be empty."],"password":["\u0027Password\u0027 must not be empty."]},"code":"Validation.Failed"}"""),
            (response.StatusCode, await AuthProblems.BodyWithoutTraceIdAsync(response)));
    }

    [Fact]
    public async Task A_password_over_256_characters_is_a_validation_error_on_the_password()
    {
        using HttpResponseMessage response = await fixture.SendAsync(
            HttpMethod.Post, "/api/v1/auth/sign-in", body: new { email = UniqueEmail(), password = new string('a', 257) });

        Assert.Equal(
            (HttpStatusCode.BadRequest, "password"),
            (response.StatusCode, string.Join(',', await AuthProblems.ErrorFieldsAsync(response))));
    }
}
