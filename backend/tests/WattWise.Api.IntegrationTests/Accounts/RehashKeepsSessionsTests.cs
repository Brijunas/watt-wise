using System.Net;

namespace WattWise.Api.IntegrationTests.Accounts;

public class RehashKeepsSessionsTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    [Fact]
    public async Task A_session_still_refreshes_after_another_sign_in_rehashed_the_password()
    {
        string email = $"user-{Guid.NewGuid():N}@example.com";
        string password = $"Aa1!{Guid.NewGuid():N}";
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        SignedInSession existing = await fixture.SignInAsync(email, password);
        await fixture.StoreOldPasswordHashAsync(ids.IdentityId, password);
        _ = await fixture.SignInAsync(email, password);

        using HttpResponseMessage refresh = await fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", cookie: existing.Cookie);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }
}
