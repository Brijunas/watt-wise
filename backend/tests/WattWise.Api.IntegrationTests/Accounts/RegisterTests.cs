using System.Net;

namespace WattWise.Api.IntegrationTests.Accounts;

public class RegisterTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string ValidPassword() => $"Aa1!{Guid.NewGuid():N}";

    [Fact]
    public async Task Registering_a_new_email_answers_202_with_an_empty_body()
    {
        string email = UniqueEmail();

        using HttpResponseMessage response = await fixture.PostAsync(
            "register", new { email, password = ValidPassword(), language = "lt" });

        Assert.Equal((HttpStatusCode.Accepted, ""), (response.StatusCode, await response.Content.ReadAsStringAsync(Token)));
    }

    [Fact]
    public async Task Registering_a_new_email_creates_an_unconfirmed_account_with_the_normalized_email_and_language()
    {
        string local = Guid.NewGuid().ToString("N");

        using HttpResponseMessage response = await fixture.PostAsync(
            "register", new { email = $" User-{local}@Example.COM ", password = ValidPassword(), language = "en" });

        Guid identityId = await fixture.IdentityIdOfAsync($"user-{local}@example.com");
        Assert.Equal($"false|en|user-{local}@example.com", await fixture.ConfirmedAndPreferredLanguageAsync(identityId));
    }

    [Fact]
    public async Task Registering_a_new_email_writes_exactly_one_pending_confirmation_request_for_the_new_account()
    {
        string email = UniqueEmail();

        using HttpResponseMessage response = await fixture.PostAsync(
            "register", new { email, password = ValidPassword(), language = "lt" });

        Guid accountId = await fixture.AccountIdOfAsync(email);
        Assert.Equal([$"EmailConfirmation|pending|{accountId}|"], await fixture.OutboxRowsForAccountAsync(accountId));
    }

    [Fact]
    public async Task Registering_a_taken_email_writes_exactly_one_pending_already_exists_request_for_the_existing_account()
    {
        string email = UniqueEmail();
        (Guid accountId, _) = await fixture.RegisterAsync(email);

        using HttpResponseMessage response = await fixture.PostAsync(
            "register", new { email = email.ToUpperInvariant(), password = ValidPassword(), language = "en" });

        Assert.Equal(
            [$"EmailConfirmation|pending|{accountId}|", $"AccountAlreadyExists|pending|{accountId}|"],
            await fixture.OutboxRowsForAccountAsync(accountId));
    }

    [Fact]
    public async Task Registering_a_taken_email_gives_the_same_status_and_body_as_a_new_one()
    {
        string takenEmail = UniqueEmail();
        await fixture.RegisterAsync(takenEmail);
        using HttpResponseMessage fresh = await fixture.PostAsync(
            "register", new { email = UniqueEmail(), password = ValidPassword(), language = "lt" });

        using HttpResponseMessage taken = await fixture.PostAsync(
            "register", new { email = takenEmail, password = ValidPassword(), language = "lt" });

        Assert.Equal(
            (fresh.StatusCode, await fresh.Content.ReadAsStringAsync(Token)),
            (taken.StatusCode, await taken.Content.ReadAsStringAsync(Token)));
    }

    [Fact]
    public async Task Registering_a_taken_email_in_other_casing_creates_no_second_account()
    {
        string email = UniqueEmail();
        await fixture.RegisterAsync(email);

        using HttpResponseMessage response = await fixture.PostAsync(
            "register", new { email = email.ToUpperInvariant(), password = ValidPassword(), language = "lt" });

        Assert.Equal(1, await fixture.IdentityCountAsync(email));
    }

    [Fact]
    public async Task Registering_a_taken_email_leaves_the_existing_password_unchanged()
    {
        string email = UniqueEmail();
        string original = ValidPassword();
        using HttpResponseMessage first = await fixture.PostAsync("register", new { email, password = original, language = "lt" });
        Guid identityId = await fixture.IdentityIdOfAsync(email);

        using HttpResponseMessage second = await fixture.PostAsync(
            "register", new { email, password = ValidPassword(), language = "lt" });

        Assert.True(await fixture.PasswordMatchesAsync(identityId, original));
    }

    [Theory]
    [InlineData("Aa1!aaaaaaaaa")] // 13 characters
    [InlineData("aa1!aaaaaaaaaaaa")] // no uppercase
    [InlineData("AA1!AAAAAAAAAAAA")] // no lowercase
    [InlineData("Aa!aaaaaaaaaaaaa")] // no digit
    [InlineData("Aa1aaaaaaaaaaaaa")] // no symbol
    public async Task A_password_breaking_the_rules_answers_400_with_a_password_field_error_and_writes_nothing(string password)
    {
        string email = UniqueEmail();

        using HttpResponseMessage response = await fixture.PostAsync("register", new { email, password, language = "lt" });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Validation.Failed", Token);
        Assert.Equal(["password"], await AuthProblems.ErrorFieldsAsync(response));
        Assert.Equal(0, await fixture.IdentityCountAsync(email));
    }

    [Fact]
    public async Task A_password_of_257_characters_answers_400_with_a_password_field_error()
    {
        using HttpResponseMessage response = await fixture.PostAsync(
            "register", new { email = UniqueEmail(), password = "Aa1!" + new string('a', 253), language = "lt" });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Validation.Failed", Token);
        Assert.Equal(["password"], await AuthProblems.ErrorFieldsAsync(response));
    }

    [Theory]
    [InlineData("", "email")]
    [InlineData("not-an-email", "email")]
    public async Task An_invalid_email_answers_400_with_an_email_field_error(string email, string field)
    {
        using HttpResponseMessage response = await fixture.PostAsync(
            "register", new { email, password = ValidPassword(), language = "lt" });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Validation.Failed", Token);
        Assert.Equal([field], await AuthProblems.ErrorFieldsAsync(response));
    }

    [Fact]
    public async Task An_email_of_255_characters_answers_400_with_an_email_field_error()
    {
        string email = new string('a', 255 - "@example.com".Length) + "@example.com";

        using HttpResponseMessage response = await fixture.PostAsync(
            "register", new { email, password = ValidPassword(), language = "lt" });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Validation.Failed", Token);
        Assert.Equal(["email"], await AuthProblems.ErrorFieldsAsync(response));
    }

    [Theory]
    [InlineData("")]
    [InlineData("xx")]
    public async Task An_unsupported_language_answers_400_with_a_language_field_error(string language)
    {
        string email = UniqueEmail();

        using HttpResponseMessage response = await fixture.PostAsync(
            "register", new { email, password = ValidPassword(), language });

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "Validation.Failed", Token);
        Assert.Equal(["language"], await AuthProblems.ErrorFieldsAsync(response));
    }

    [Fact]
    public async Task A_new_registration_writes_a_pending_request_due_now_keyed_by_the_account_only()
    {
        string email = UniqueEmail();

        using HttpResponseMessage response = await fixture.PostAsync(
            "register", new { email, password = ValidPassword(), language = "lt" });

        Guid accountId = await fixture.AccountIdOfAsync(email);
        Assert.Equal(["pending|0|true|true|1"], await fixture.OutboxStateForAccountAsync(accountId));
    }

    [Fact]
    public async Task Concurrent_registrations_of_one_new_email_all_answer_202_and_create_one_account()
    {
        List<string> rounds = [];
        for (int round = 0; round < 5; round++)
        {
            string email = UniqueEmail();
            string password = ValidPassword();

            HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(
                _ => fixture.PostAsync("register", new { email, password, language = "lt" })));

            string statuses = string.Join(',', responses.Select(r => $"{(int)r.StatusCode}:{r.Content.ReadAsStringAsync(Token).Result}"));
            rounds.Add($"{statuses}/{await fixture.IdentityCountAsync(email)}");
        }

        Assert.Equal(Enumerable.Repeat("202:,202:,202:,202:/1", 5), rounds);
    }

    [Theory]
    [InlineData(14, 49)] // shortest password
    [InlineData(256, 49)] // longest password
    [InlineData(40, 254)] // longest email
    public async Task A_password_or_email_of_exactly_the_limit_is_accepted(int passwordLength, int emailLength)
    {
        string email = EmailOfLength(emailLength);
        string password = "Aa1!" + new string('a', passwordLength - 4);
        Assert.Equal((emailLength, passwordLength), (email.Length, password.Length));

        using HttpResponseMessage response = await fixture.PostAsync("register", new { email, password, language = "lt" });

        Assert.Equal((HttpStatusCode.Accepted, 1), (response.StatusCode, await fixture.IdentityCountAsync(email)));
    }

    /// <summary>A unique, valid address of exactly <paramref name="length"/> characters: 32 hex characters, '@', then labels of at most 63 characters ending in ".com".</summary>
    private static string EmailOfLength(int length)
    {
        int prefixLength = length - 32 - 1 - ".com".Length;
        char[] prefix = new char[prefixLength];
        for (int i = 0; i < prefixLength; i++)
        {
            prefix[i] = (i + 1) % 64 == 0 ? '.' : 'x';
        }

        return $"{Guid.NewGuid():N}@{new string(prefix)}.com";
    }
}
