using System.Net;

namespace WattWise.Api.IntegrationTests.Accounts;

public class ResendConfirmationTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Resending_for_a_known_address_answers_202_with_an_empty_body()
    {
        string email = UniqueEmail();
        await fixture.RegisterAsync(email);

        using HttpResponseMessage response = await fixture.PostAsync("resend-confirmation", new { email });

        Assert.Equal((HttpStatusCode.Accepted, ""), (response.StatusCode, await response.Content.ReadAsStringAsync(Token)));
    }

    [Fact]
    public async Task Resending_for_an_unknown_address_gives_the_same_status_and_body_as_for_a_known_one()
    {
        string known = UniqueEmail();
        await fixture.RegisterAsync(known);
        using HttpResponseMessage forKnown = await fixture.PostAsync("resend-confirmation", new { email = known });

        using HttpResponseMessage forUnknown = await fixture.PostAsync("resend-confirmation", new { email = UniqueEmail() });

        Assert.Equal(
            (forKnown.StatusCode, await forKnown.Content.ReadAsStringAsync(Token)),
            (forUnknown.StatusCode, await forUnknown.Content.ReadAsStringAsync(Token)));
    }

    [Fact]
    public async Task Resending_writes_one_pending_request_keyed_by_the_normalized_address_not_the_account()
    {
        string local = Guid.NewGuid().ToString("N");
        await fixture.RegisterAsync($"user-{local}@example.com");

        using HttpResponseMessage response = await fixture.PostAsync(
            "resend-confirmation", new { email = $" User-{local}@Example.COM " });

        Assert.Equal(
            [$"EmailConfirmation|pending||user-{local}@example.com"],
            await fixture.OutboxRowsForEmailAsync($"user-{local}@example.com"));
    }

    [Fact]
    public async Task Resending_writes_a_pending_request_due_now_keyed_by_the_address_only()
    {
        string email = UniqueEmail();

        using HttpResponseMessage response = await fixture.PostAsync("resend-confirmation", new { email });

        Assert.Equal(["pending|0|true|true|1"], await fixture.OutboxStateForEmailAsync(email));
    }

    [Fact]
    public async Task Resending_for_an_address_without_an_account_still_writes_a_request_keyed_by_the_address()
    {
        string email = UniqueEmail();

        using HttpResponseMessage response = await fixture.PostAsync("resend-confirmation", new { email });

        Assert.Equal([$"EmailConfirmation|pending||{email}"], await fixture.OutboxRowsForEmailAsync(email));
    }

    [Fact]
    public async Task Resending_for_a_confirmed_account_still_writes_a_request_keyed_by_the_address()
    {
        string email = UniqueEmail();
        (_, Guid identityId) = await fixture.RegisterAsync(email);
        string token = await fixture.IssueConfirmationTokenAsync(identityId);
        using HttpResponseMessage confirm = await fixture.PostAsync("confirm-email", new { id = identityId, token });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        using HttpResponseMessage response = await fixture.PostAsync("resend-confirmation", new { email });

        Assert.Equal([$"EmailConfirmation|pending||{email}"], await fixture.OutboxRowsForEmailAsync(email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task An_invalid_email_answers_400_with_an_email_field_error_and_writes_no_request(string email)
    {
        using HttpResponseMessage response = await fixture.PostAsync("resend-confirmation", new { email });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Validation.Failed", Token);
        Assert.Equal(["email"], await AuthProblems.ErrorFieldsAsync(response));
    }
}
