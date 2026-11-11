using System.Net;

namespace WattWise.Api.IntegrationTests.Accounts;

public class ConfirmEmailTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task A_valid_link_answers_204_and_confirms_the_account()
    {
        string email = UniqueEmail();
        (_, Guid identityId) = await fixture.RegisterAsync(email);
        string token = await fixture.IssueConfirmationTokenAsync(identityId);

        using HttpResponseMessage response = await fixture.PostAsync("confirm-email", new { id = identityId, token });

        Assert.Equal(
            (HttpStatusCode.NoContent, $"true|lt|{email}"),
            (response.StatusCode, await fixture.ConfirmedAndPreferredLanguageAsync(identityId)));
    }

    [Fact]
    public async Task A_second_use_of_the_link_fails_with_InvalidEmailLink()
    {
        (_, Guid identityId) = await fixture.RegisterAsync(UniqueEmail());
        string token = await fixture.IssueConfirmationTokenAsync(identityId);
        using HttpResponseMessage first = await fixture.PostAsync("confirm-email", new { id = identityId, token });

        using HttpResponseMessage second = await fixture.PostAsync("confirm-email", new { id = identityId, token });

        await ProblemDetailsAssertions.AssertProblemAsync(second, HttpStatusCode.BadRequest, "Accounts.InvalidEmailLink", Token);
    }

    [Fact]
    public async Task An_altered_link_fails_with_InvalidEmailLink_and_does_not_confirm()
    {
        string email = UniqueEmail();
        (_, Guid identityId) = await fixture.RegisterAsync(email);
        string token = await fixture.IssueConfirmationTokenAsync(identityId);
        string altered = (token[0] == 'A' ? 'B' : 'A') + token[1..];

        using HttpResponseMessage response = await fixture.PostAsync("confirm-email", new { id = identityId, token = altered });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Accounts.InvalidEmailLink", Token);
        Assert.Equal($"false|lt|{email}", await fixture.ConfirmedAndPreferredLanguageAsync(identityId));
    }

    [Fact]
    public async Task A_failed_attempt_does_not_use_up_the_real_link()
    {
        (_, Guid identityId) = await fixture.RegisterAsync(UniqueEmail());
        string token = await fixture.IssueConfirmationTokenAsync(identityId);
        string altered = (token[0] == 'A' ? 'B' : 'A') + token[1..];
        using HttpResponseMessage failed = await fixture.PostAsync("confirm-email", new { id = identityId, token = altered });

        using HttpResponseMessage response = await fixture.PostAsync("confirm-email", new { id = identityId, token });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task A_password_reset_link_fails_with_InvalidEmailLink_and_does_not_confirm()
    {
        string email = UniqueEmail();
        (_, Guid identityId) = await fixture.RegisterAsync(email);
        string resetToken = await fixture.IssuePasswordResetTokenAsync(identityId);

        using HttpResponseMessage response = await fixture.PostAsync("confirm-email", new { id = identityId, token = resetToken });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Accounts.InvalidEmailLink", Token);
        Assert.Equal($"false|lt|{email}", await fixture.ConfirmedAndPreferredLanguageAsync(identityId));
    }

    [Fact]
    public async Task An_empty_token_fails_with_InvalidEmailLink()
    {
        (_, Guid identityId) = await fixture.RegisterAsync(UniqueEmail());

        using HttpResponseMessage response = await fixture.PostAsync("confirm-email", new { id = identityId, token = "" });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Accounts.InvalidEmailLink", Token);
    }

    [Fact]
    public async Task Another_accounts_link_fails_with_InvalidEmailLink_and_leaves_both_unconfirmed()
    {
        string emailA = UniqueEmail();
        string emailB = UniqueEmail();
        (_, Guid identityA) = await fixture.RegisterAsync(emailA);
        (_, Guid identityB) = await fixture.RegisterAsync(emailB);
        string tokenOfB = await fixture.IssueConfirmationTokenAsync(identityB);

        using HttpResponseMessage response = await fixture.PostAsync("confirm-email", new { id = identityA, token = tokenOfB });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Accounts.InvalidEmailLink", Token);
        Assert.Equal(
            [$"false|lt|{emailA}", $"false|lt|{emailB}"],
            new[] { await fixture.ConfirmedAndPreferredLanguageAsync(identityA), await fixture.ConfirmedAndPreferredLanguageAsync(identityB) });
    }

    [Fact]
    public async Task An_unknown_account_id_gives_the_same_body_as_a_wrong_token()
    {
        (_, Guid identityId) = await fixture.RegisterAsync(UniqueEmail());
        string token = await fixture.IssueConfirmationTokenAsync(identityId);
        string altered = (token[0] == 'A' ? 'B' : 'A') + token[1..];
        using HttpResponseMessage wrongToken = await fixture.PostAsync("confirm-email", new { id = identityId, token = altered });

        using HttpResponseMessage unknownId = await fixture.PostAsync("confirm-email", new { id = Guid.NewGuid(), token });

        Assert.Equal(
            (wrongToken.StatusCode, await AuthProblems.BodyWithoutTraceIdAsync(wrongToken)),
            (unknownId.StatusCode, await AuthProblems.BodyWithoutTraceIdAsync(unknownId)));
    }

    [Fact]
    public async Task An_unknown_account_id_fails_with_InvalidEmailLink()
    {
        using HttpResponseMessage response = await fixture.PostAsync(
            "confirm-email", new { id = Guid.NewGuid(), token = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Accounts.InvalidEmailLink", Token);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"id\":\"not-a-guid\",\"token\":\"x\"}")]
    [InlineData("{\"id\":\"00000000-0000-0000-0000-000000000001\"}")]
    public async Task A_malformed_request_answers_400_Validation_Failed(string json)
    {
        using StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await fixture.PostRawAsync("confirm-email", content);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Validation.Failed", Token);
    }
}
