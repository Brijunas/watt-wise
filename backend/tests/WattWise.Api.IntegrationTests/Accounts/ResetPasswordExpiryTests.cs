using System.Net;

using NodaTime;

namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>Moves the fixture's clock, so it has a fixture (and clock) of its own; every test issues its link after any earlier move.</summary>
public class ResetPasswordExpiryTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(Guid IdentityId, string Token, string Password)> SeedAsync()
    {
        string password = $"Aa1!{Guid.NewGuid():N}";
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync($"user-{Guid.NewGuid():N}@example.com", password);
        return (ids.IdentityId, await fixture.IssuePasswordResetTokenAsync(ids.IdentityId), password);
    }

    [Fact]
    public async Task A_link_works_one_second_before_the_hour_ends()
    {
        (Guid identityId, string token, _) = await SeedAsync();
        fixture.Clock.Advance(Duration.FromHours(1) - Duration.FromSeconds(1));

        using HttpResponseMessage response = await fixture.PostAsync(
            "reset-password", new { id = identityId, token, newPassword = $"Aa1!{Guid.NewGuid():N}" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task A_link_fails_with_InvalidEmailLink_once_an_hour_has_passed_and_the_password_stays()
    {
        (Guid identityId, string token, string password) = await SeedAsync();
        fixture.Clock.Advance(Duration.FromHours(1));

        using HttpResponseMessage response = await fixture.PostAsync(
            "reset-password", new { id = identityId, token, newPassword = $"Aa1!{Guid.NewGuid():N}" });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Accounts.InvalidEmailLink", Token);
        Assert.True(await fixture.PasswordMatchesAsync(identityId, password));
    }
}
