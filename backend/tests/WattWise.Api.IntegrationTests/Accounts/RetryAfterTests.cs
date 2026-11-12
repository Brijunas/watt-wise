using System.Net;
using System.Text.Json;

using NodaTime;

namespace WattWise.Api.IntegrationTests.Accounts;

public sealed class RetryAfterTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("sign-in")]
    [InlineData("change-password")]
    public async Task A_locked_account_answers_423_with_the_remaining_seconds(string endpoint)
    {
        string email = $"user-{Guid.NewGuid():N}@example.com";
        string password = $"Aa1!{Guid.NewGuid():N}";
        await fixture.CreateAccountAsync(email, password);
        SignedInSession session = await fixture.SignInAsync(email, password);
        for (int attempt = 0; attempt < 5; attempt++)
        {
            using HttpResponseMessage wrong = await fixture.PostAsync("sign-in", new { email, password = "wrong-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        fixture.Clock.Advance(Duration.FromMilliseconds(1250));
        using HttpResponseMessage response = await fixture.SendAsync(
            HttpMethod.Post, $"/api/v1/auth/{endpoint}", bearer: session.AccessToken,
            body: new { email, password, currentPassword = password, newPassword = $"Bb2!{Guid.NewGuid():N}" });

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal(
            (HttpStatusCode.Locked, "Accounts.LockedOut", "899"),
            (response.StatusCode, body.RootElement.GetProperty("code").GetString(),
                string.Join(',', response.Headers.GetValues("Retry-After"))));
    }

    [Theory]
    [InlineData("sign-in", "wrong-password", 401, "Accounts.InvalidCredentials")]
    [InlineData("change-password", "wrong-password", 400, "Accounts.InvalidCurrentPassword")]
    public async Task Other_password_errors_do_not_include_retry_after(string endpoint, string suppliedPassword, int status, string code)
    {
        string email = $"user-{Guid.NewGuid():N}@example.com";
        string password = $"Aa1!{Guid.NewGuid():N}";
        await fixture.CreateAccountAsync(email, password);
        SignedInSession session = await fixture.SignInAsync(email, password);

        using HttpResponseMessage response = await fixture.SendAsync(
            HttpMethod.Post, $"/api/v1/auth/{endpoint}", bearer: session.AccessToken,
            body: new { email, password = suppliedPassword, currentPassword = suppliedPassword, newPassword = $"Bb2!{Guid.NewGuid():N}" });

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal((status, code, false), ((int)response.StatusCode, body.RootElement.GetProperty("code").GetString(), response.Headers.Contains("Retry-After")));
    }
}
