using System.Net;

namespace WattWise.Api.IntegrationTests.Accounts;

public class ForgotPasswordTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Asking_for_a_known_address_answers_202_with_an_empty_body()
    {
        string email = UniqueEmail();
        await fixture.CreateAccountAsync(email, "Aa1!" + Guid.NewGuid().ToString("N"));

        using HttpResponseMessage response = await fixture.PostAsync("forgot-password", new { email });

        Assert.Equal((HttpStatusCode.Accepted, ""), (response.StatusCode, await response.Content.ReadAsStringAsync(Token)));
    }

    [Fact]
    public async Task Asking_for_an_unknown_address_gives_the_same_status_and_body_as_for_a_known_one()
    {
        string known = UniqueEmail();
        await fixture.CreateAccountAsync(known, "Aa1!" + Guid.NewGuid().ToString("N"));
        using HttpResponseMessage forKnown = await fixture.PostAsync("forgot-password", new { email = known });

        using HttpResponseMessage forUnknown = await fixture.PostAsync("forgot-password", new { email = UniqueEmail() });

        Assert.Equal(
            (forKnown.StatusCode, await forKnown.Content.ReadAsStringAsync(Token)),
            (forUnknown.StatusCode, await forUnknown.Content.ReadAsStringAsync(Token)));
    }

    [Fact]
    public async Task Asking_for_a_known_address_writes_one_pending_request_keyed_by_the_normalized_address_and_no_account()
    {
        string local = Guid.NewGuid().ToString("N");
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync($"user-{local}@example.com", "Aa1!" + Guid.NewGuid().ToString("N"));

        using HttpResponseMessage response = await fixture.PostAsync("forgot-password", new { email = $" User-{local}@Example.COM " });

        Assert.Equal(
            ($"PasswordReset|pending||user-{local}@example.com", string.Empty),
            (
                string.Join(';', await fixture.OutboxRowsForEmailAsync($"user-{local}@example.com")),
                string.Join(';', await fixture.OutboxRowsForAccountAsync(ids.AccountId))));
    }

    [Fact]
    public async Task Asking_for_an_address_without_an_account_still_writes_a_request_keyed_by_the_address()
    {
        string email = UniqueEmail();

        using HttpResponseMessage response = await fixture.PostAsync("forgot-password", new { email });

        Assert.Equal([$"PasswordReset|pending||{email}"], await fixture.OutboxRowsForEmailAsync(email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task An_invalid_email_answers_400_with_an_email_field_error(string email)
    {
        using HttpResponseMessage response = await fixture.PostAsync("forgot-password", new { email });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Validation.Failed", Token);
        Assert.Equal(["email"], await AuthProblems.ErrorFieldsAsync(response));
    }
}
