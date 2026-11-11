using System.Net;
using System.Text.Json;

using NodaTime;

namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>Moves the fixture's clock, so it has a fixture (and clock) of its own.</summary>
public class LockoutApiTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string ValidPassword() => $"Aa1!{Guid.NewGuid():N}";

    private Task<HttpResponseMessage> SignInAsync(string email, string password) =>
        fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/sign-in", body: new { email, password });

    private async Task LockAsync(string email)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            using HttpResponseMessage response = await SignInAsync(email, ValidPassword());
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Five_wrong_passwords_in_a_row_each_answer_401_the_fifth_included()
    {
        string email = UniqueEmail();
        await fixture.CreateAccountAsync(email, ValidPassword());
        List<int> statuses = [];
        for (int attempt = 0; attempt < 5; attempt++)
        {
            using HttpResponseMessage failed = await SignInAsync(email, ValidPassword());
            statuses.Add((int)failed.StatusCode);
        }

        Assert.Equal("401,401,401,401,401", string.Join(',', statuses));
    }

    [Fact]
    public async Task The_sixth_attempt_with_the_right_password_answers_423_LockedOut_with_the_Locked_type_and_title()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        await fixture.CreateAccountAsync(email, password);
        await LockAsync(email);

        using HttpResponseMessage sixth = await SignInAsync(email, password);

        using JsonDocument body = JsonDocument.Parse(await sixth.Content.ReadAsStringAsync(Token));
        Assert.Equal(
            (423, "Accounts.LockedOut", "https://www.rfc-editor.org/rfc/rfc4918#section-11.3", "Locked", "application/problem+json"),
            (
                body.RootElement.GetProperty("status").GetInt32(),
                body.RootElement.GetProperty("code").GetString(),
                body.RootElement.GetProperty("type").GetString(),
                body.RootElement.GetProperty("title").GetString(),
                sixth.Content.Headers.ContentType?.MediaType));
    }

    [Fact]
    public async Task Four_wrong_passwords_do_not_lock_the_account()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        await fixture.CreateAccountAsync(email, password);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            using HttpResponseMessage failed = await SignInAsync(email, ValidPassword());
        }

        using HttpResponseMessage response = await SignInAsync(email, password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_account_is_still_locked_one_second_before_15_minutes_have_passed()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        await fixture.CreateAccountAsync(email, password);
        await LockAsync(email);
        fixture.Clock.Advance(Duration.FromMinutes(15) - Duration.FromSeconds(1));

        using HttpResponseMessage response = await SignInAsync(email, password);

        Assert.Equal(HttpStatusCode.Locked, response.StatusCode);
    }

    [Fact]
    public async Task The_account_signs_in_again_once_15_minutes_have_passed()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        await fixture.CreateAccountAsync(email, password);
        await LockAsync(email);
        fixture.Clock.Advance(Duration.FromMinutes(15) + Duration.FromSeconds(1));

        using HttpResponseMessage response = await SignInAsync(email, password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_session_signed_in_before_the_lock_can_still_refresh_while_the_account_is_locked_out()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        await fixture.CreateAccountAsync(email, password);
        SignedInSession session = await fixture.SignInAsync(email, password);
        await LockAsync(email);

        using HttpResponseMessage response = await fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", cookie: session.Cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
