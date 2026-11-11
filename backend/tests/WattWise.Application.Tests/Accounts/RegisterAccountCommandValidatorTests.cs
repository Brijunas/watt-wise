using FluentValidation.Results;

using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

public class RegisterAccountCommandValidatorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(string, string)[]> ErrorsOfAsync(RegisterAccountCommand command, params string[] policyMessages)
    {
        ValidationResult result = await new RegisterAccountCommandValidator(new FixedPasswordPolicy([], policyMessages)).ValidateAsync(command, Token);
        return [.. result.Errors.Select(error => (error.PropertyName, error.ErrorMessage))];
    }

    [Fact]
    public async Task A_complete_command_has_no_errors()
    {
        Assert.Empty(await ErrorsOfAsync(new RegisterAccountCommand("ada@example.com", "Aa1!secret", "lt")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_missing_email_is_rejected_as_not_empty(string? email)
    {
        Assert.Equal(
            [("Email", "'Email' must not be empty.")],
            await ErrorsOfAsync(new RegisterAccountCommand(email, "Aa1!secret", "lt")));
    }

    [Fact]
    public async Task An_email_that_is_not_an_address_is_rejected()
    {
        Assert.Equal(
            [("Email", "'Email' is not a valid email address.")],
            await ErrorsOfAsync(new RegisterAccountCommand("not-an-email", "Aa1!secret", "lt")));
    }

    [Fact]
    public async Task An_email_of_254_characters_is_accepted()
    {
        string email = new string('a', 254 - "@example.com".Length) + "@example.com";

        Assert.Empty(await ErrorsOfAsync(new RegisterAccountCommand(email, "Aa1!secret", "lt")));
    }

    [Fact]
    public async Task An_email_of_255_characters_is_rejected_for_its_length_only()
    {
        string email = new string('a', 255 - "@example.com".Length) + "@example.com";

        Assert.Equal(
            [("Email", "'Email' must be at most 254 characters.")],
            await ErrorsOfAsync(new RegisterAccountCommand(email, "Aa1!secret", "lt")));
    }

    [Fact]
    public async Task Spaces_around_an_email_of_254_characters_do_not_count()
    {
        string email = " " + new string('a', 254 - "@example.com".Length) + "@example.com ";

        Assert.Empty(await ErrorsOfAsync(new RegisterAccountCommand(email, "Aa1!secret", "lt")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_missing_password_is_rejected_without_asking_the_policy(string? password)
    {
        Assert.Equal(
            [("Password", "'Password' must not be empty.")],
            await ErrorsOfAsync(new RegisterAccountCommand("ada@example.com", password, "lt"), "policy was asked"));
    }

    [Fact]
    public async Task Each_message_of_the_password_policy_becomes_an_error_on_the_password()
    {
        Assert.Equal(
            [("Password", "too short"), ("Password", "no symbol")],
            await ErrorsOfAsync(new RegisterAccountCommand("ada@example.com", "x", "lt"), "too short", "no symbol"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("xx")]
    [InlineData("LT")]
    public async Task A_language_other_than_lt_or_en_is_rejected(string? language)
    {
        Assert.Equal(
            [("Language", "'Language' must be 'lt' or 'en'.")],
            await ErrorsOfAsync(new RegisterAccountCommand("ada@example.com", "Aa1!secret", language)));
    }

    [Fact]
    public async Task The_language_en_is_accepted()
    {
        Assert.Empty(await ErrorsOfAsync(new RegisterAccountCommand("ada@example.com", "Aa1!secret", "en")));
    }
}
