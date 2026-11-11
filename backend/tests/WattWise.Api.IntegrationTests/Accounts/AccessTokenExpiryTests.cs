using System.Net;

using NodaTime;

namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>Moves the fixture's clock, so it has a fixture (and clock) of its own; every test signs in after any earlier move.</summary>
public class AccessTokenExpiryTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private async Task<string> SignedInTokenAsync()
    {
        string email = $"user-{Guid.NewGuid():N}@example.com";
        string password = $"Aa1!{Guid.NewGuid():N}";
        await fixture.CreateAccountAsync(email, password);
        return (await fixture.SignInAsync(email, password)).AccessToken;
    }

    [Fact]
    public async Task The_access_token_still_works_one_second_before_15_minutes_have_passed()
    {
        string token = await SignedInTokenAsync();
        fixture.Clock.Advance(Duration.FromMinutes(15) - Duration.FromSeconds(1));

        using HttpResponseMessage response = await fixture.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_access_token_is_refused_once_15_minutes_have_passed()
    {
        string token = await SignedInTokenAsync();
        fixture.Clock.Advance(Duration.FromMinutes(15));

        using HttpResponseMessage response = await fixture.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: token);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "General.Unauthorized", TestContext.Current.CancellationToken);
    }
}
