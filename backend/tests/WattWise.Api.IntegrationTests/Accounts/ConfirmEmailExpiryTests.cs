using System.Net;

using NodaTime;

namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>Moves the fixture's clock, so it has a fixture (and clock) of its own; every test issues its link after any earlier move.</summary>
public class ConfirmEmailExpiryTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_link_works_one_second_before_the_24_hours_end()
    {
        (_, Guid identityId) = await fixture.RegisterAsync($"user-{Guid.NewGuid():N}@example.com");
        string token = await fixture.IssueConfirmationTokenAsync(identityId);
        fixture.Clock.Advance(Duration.FromHours(24) - Duration.FromSeconds(1));

        using HttpResponseMessage response = await fixture.PostAsync("confirm-email", new { id = identityId, token });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task A_link_fails_with_InvalidEmailLink_once_24_hours_have_passed()
    {
        string email = $"user-{Guid.NewGuid():N}@example.com";
        (_, Guid identityId) = await fixture.RegisterAsync(email);
        string token = await fixture.IssueConfirmationTokenAsync(identityId);
        fixture.Clock.Advance(Duration.FromHours(24));

        using HttpResponseMessage response = await fixture.PostAsync("confirm-email", new { id = identityId, token });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Accounts.InvalidEmailLink", Token);
    }
}
