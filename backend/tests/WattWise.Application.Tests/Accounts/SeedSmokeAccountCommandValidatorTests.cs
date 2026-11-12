using FluentValidation.Results;

using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

public class SeedSmokeAccountCommandValidatorTests
{
    private static async Task<(string, string)[]> ErrorsOfAsync(string? email, string? password, params string[] policyMessages)
    {
        ValidationResult result = await new SeedSmokeAccountCommandValidator(new FixedPasswordPolicy([], policyMessages))
            .ValidateAsync(new SeedSmokeAccountCommand(email, password, Admin: false), TestContext.Current.CancellationToken);
        return [.. result.Errors.Select(error => (error.PropertyName, error.ErrorMessage))];
    }

    [Fact]
    public async Task A_valid_email_and_a_password_the_policy_accepts_have_no_errors()
    {
        Assert.Empty(await ErrorsOfAsync("ada@example.com", "Aa1!secret"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_missing_email_is_rejected_as_not_empty(string? email)
    {
        Assert.Equal([("Email", "'Email' must not be empty.")], await ErrorsOfAsync(email, "Aa1!secret"));
    }

    [Fact]
    public async Task An_email_that_is_not_an_address_is_rejected()
    {
        Assert.Equal([("Email", "'Email' is not a valid email address.")], await ErrorsOfAsync("not-an-email", "Aa1!secret"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_missing_password_is_rejected_as_not_empty_without_asking_the_policy(string? password)
    {
        // The policy would add "Too short." for an empty password; the cascade stops before it.
        Assert.Equal([("Password", "'Password' must not be empty.")], await ErrorsOfAsync("ada@example.com", password, "Too short."));
    }

    [Fact]
    public async Task Each_message_of_the_password_policy_is_an_error_on_Password()
    {
        Assert.Equal(
            [("Password", "Too short."), ("Password", "Needs a digit.")],
            await ErrorsOfAsync("ada@example.com", "abc", "Too short.", "Needs a digit."));
    }
}
