using System.Globalization;
using System.Text.Json;

using NodaTime;

namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>What the session endpoint tests share: accounts that sign in several times, the three calls and a plain-text view of the list.</summary>
internal static class SessionsApi
{
    public const string FirefoxOnLinux = "Mozilla/5.0 (X11; Linux x86_64; rv:130.0) Gecko/20100101 Firefox/130.0";

    public const string ChromeOnWindows = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";

    public static async Task<SessionCredentials> NewAccountAsync(AccountsApiFixture fixture)
    {
        string email = $"user-{Guid.NewGuid():N}@example.com";
        string password = $"Aa1!{Guid.NewGuid():N}";
        return new SessionCredentials(await fixture.CreateAccountAsync(email, password), email, password);
    }

    /// <summary>The id of the session an access token belongs to, from its <c>sid</c> claim.</summary>
    public static string SessionIdOf(SignedInSession session) => TestJwt.Read(session.AccessToken).GetClaim("sid").Value;

    public static Task<HttpResponseMessage> ListAsync(AccountsApiFixture fixture, string? bearer) =>
        fixture.SendAsync(HttpMethod.Get, "/api/v1/auth/sessions", bearer: bearer);

    public static Task<HttpResponseMessage> EndAsync(AccountsApiFixture fixture, string? bearer, string sessionId) =>
        fixture.SendAsync(HttpMethod.Delete, $"/api/v1/auth/sessions/{sessionId}", bearer: bearer);

    public static Task<HttpResponseMessage> SignOutEverywhereAsync(AccountsApiFixture fixture, string? bearer) =>
        fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/sign-out-everywhere", bearer: bearer);

    public static Task<HttpResponseMessage> RefreshAsync(AccountsApiFixture fixture, string cookie) =>
        fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", cookie: cookie);

    /// <summary>An instant as the list shows it: <c>2026-11-11T12:00:00Z</c>.</summary>
    public static string Stamp(Instant instant) => instant.ToDateTimeUtc().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>Each listed session as <c>id|deviceLabel|signedInAt|lastUsedAt|current</c>, in the order listed.</summary>
    public static async Task<string[]> ListedAsync(HttpResponseMessage response)
    {
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return
        [
            .. body.RootElement.EnumerateArray().Select(session =>
                $"{session.GetProperty("id").GetGuid()}|{session.GetProperty("deviceLabel").GetString()}"
                + $"|{Stamp(Instant.FromDateTimeOffset(session.GetProperty("signedInAt").GetDateTimeOffset()))}"
                + $"|{Stamp(Instant.FromDateTimeOffset(session.GetProperty("lastUsedAt").GetDateTimeOffset()))}"
                + $"|{session.GetProperty("current").GetBoolean()}"),
        ];
    }
}
