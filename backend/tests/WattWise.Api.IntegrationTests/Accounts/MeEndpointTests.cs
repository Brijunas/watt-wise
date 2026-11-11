using System.Net;

namespace WattWise.Api.IntegrationTests.Accounts;

public class MeEndpointTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string ValidPassword() => $"Aa1!{Guid.NewGuid():N}";

    private Task<HttpResponseMessage> MeAsync(string? bearer) => fixture.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: bearer);

    private static readonly Dictionary<string, Func<string>> RefusedTokenCases = new()
    {
        ["wrong key"] = () => TestJwt.Mint(Guid.NewGuid().ToString(), key: new string('x', 64)),
        ["wrong issuer"] = () => TestJwt.Mint(Guid.NewGuid().ToString(), issuer: "someone-else"),
        ["wrong audience"] = () => TestJwt.Mint(Guid.NewGuid().ToString(), audience: "someone-else"),
        ["unsigned"] = () => TestJwt.Mint(Guid.NewGuid().ToString(), signed: false),
        ["no expiry"] = () => TestJwt.Mint(Guid.NewGuid().ToString(), withExpiry: false),
        ["expired"] = () => TestJwt.Mint(Guid.NewGuid().ToString(), expires: new DateTime(2026, 11, 11, 11, 59, 59, DateTimeKind.Utc)),
        ["not a token"] = () => "not-a-token",
    };

    public static TheoryData<string> RefusedTokens => [.. RefusedTokenCases.Keys];

    [Fact]
    public async Task A_valid_access_token_answers_200_with_the_email_language_roles_and_permissions()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        string prefix = Guid.NewGuid().ToString("N");
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        await fixture.GrantAsync(ids.AccountId, $"{prefix}-admin", $"{prefix}.write", $"{prefix}.read");
        SignedInSession session = await fixture.SignInAsync(email, password);

        using HttpResponseMessage response = await MeAsync(session.AccessToken);

        Assert.Equal(
            (HttpStatusCode.OK, $$"""{"email":"{{email}}","language":"lt","roles":["{{prefix}}-admin"],"permissions":["{{prefix}}.read","{{prefix}}.write"]}"""),
            (response.StatusCode, await response.Content.ReadAsStringAsync(Token)));
    }

    [Fact]
    public async Task An_account_without_roles_has_empty_role_and_permission_lists()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        await fixture.CreateAccountAsync(email, password);
        SignedInSession session = await fixture.SignInAsync(email, password);

        using HttpResponseMessage response = await MeAsync(session.AccessToken);

        Assert.Equal($$"""{"email":"{{email}}","language":"lt","roles":[],"permissions":[]}""", await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task A_token_minted_with_the_configured_key_issuer_and_audience_is_accepted()
    {
        string email = UniqueEmail();
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, ValidPassword());

        using HttpResponseMessage response = await MeAsync(TestJwt.Mint(ids.AccountId.ToString()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task No_token_answers_401_ProblemDetails()
    {
        using HttpResponseMessage response = await MeAsync(null);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "General.Unauthorized", Token);
    }

    [Theory]
    [MemberData(nameof(RefusedTokens))]
    public async Task A_token_with_a_wrong_key_issuer_audience_or_expiry_or_not_a_token_answers_401_ProblemDetails(string caseName)
    {
        using HttpResponseMessage response = await MeAsync(RefusedTokenCases[caseName]());

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "General.Unauthorized", Token);
    }

    [Fact]
    public async Task A_tampered_signature_answers_401_ProblemDetails()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        await fixture.CreateAccountAsync(email, password);
        SignedInSession session = await fixture.SignInAsync(email, password);
        string[] parts = session.AccessToken.Split('.');
        // Change the first signature character; the last one has bits the encoding does not use.
        string tampered = $"{parts[0]}.{parts[1]}.{(parts[2][0] == 'A' ? 'B' : 'A')}{parts[2][1..]}";

        using HttpResponseMessage response = await MeAsync(tampered);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "General.Unauthorized", Token);
    }

    [Fact]
    public async Task A_payload_swapped_in_from_another_accounts_token_answers_401_ProblemDetails()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        string otherEmail = UniqueEmail();
        await fixture.CreateAccountAsync(email, password);
        await fixture.CreateAccountAsync(otherEmail, password);
        SignedInSession mine = await fixture.SignInAsync(email, password);
        SignedInSession other = await fixture.SignInAsync(otherEmail, password);
        string forged = $"{mine.AccessToken.Split('.')[0]}.{other.AccessToken.Split('.')[1]}.{mine.AccessToken.Split('.')[2]}";

        using HttpResponseMessage response = await MeAsync(forged);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "General.Unauthorized", Token);
    }

    [Fact]
    public async Task A_validly_signed_token_whose_subject_is_not_an_id_answers_401_ProblemDetails()
    {
        using HttpResponseMessage response = await MeAsync(TestJwt.Mint("not-a-guid"));

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "General.Unauthorized", Token);
    }

    [Fact]
    public async Task A_valid_token_of_an_account_that_no_longer_exists_answers_404_AccountNotFound()
    {
        using HttpResponseMessage response = await MeAsync(TestJwt.Mint(Guid.NewGuid().ToString()));

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.NotFound, "Accounts.AccountNotFound", Token);
    }

    [Fact]
    public async Task The_challenge_of_an_expired_token_says_only_Bearer_with_no_error_description()
    {
        string expired = TestJwt.Mint(Guid.NewGuid().ToString(), expires: new DateTime(2026, 11, 11, 11, 59, 59, DateTimeKind.Utc));

        using HttpResponseMessage response = await MeAsync(expired);

        Assert.Equal("Bearer", string.Join(", ", response.Headers.WwwAuthenticate.Select(challenge => challenge.ToString())));
    }

    [Fact]
    public async Task The_challenge_of_a_wrong_audience_token_says_only_Bearer_with_no_error_description()
    {
        using HttpResponseMessage response = await MeAsync(TestJwt.Mint(Guid.NewGuid().ToString(), audience: "someone-else"));

        Assert.Equal("Bearer", string.Join(", ", response.Headers.WwwAuthenticate.Select(challenge => challenge.ToString())));
    }

    [Fact]
    public async Task One_accounts_me_does_not_list_another_accounts_roles_or_permissions()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        string prefix = Guid.NewGuid().ToString("N");
        AccountSeeder.Ids mine = await fixture.CreateAccountAsync(email, password);
        AccountSeeder.Ids other = await fixture.CreateAccountAsync(UniqueEmail(), password);
        await fixture.GrantAsync(mine.AccountId, $"{prefix}-mine", $"{prefix}.mine");
        await fixture.GrantAsync(other.AccountId, $"{prefix}-other", $"{prefix}.other");
        SignedInSession session = await fixture.SignInAsync(email, password);

        using HttpResponseMessage response = await MeAsync(session.AccessToken);

        Assert.Equal(
            $$"""{"email":"{{email}}","language":"lt","roles":["{{prefix}}-mine"],"permissions":["{{prefix}}.mine"]}""",
            await response.Content.ReadAsStringAsync(Token));
    }
}
