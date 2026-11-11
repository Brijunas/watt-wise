using FluentValidation.Results;

using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

public class ResendConfirmationCommandValidatorTests
{
    private static async Task<(string, string)[]> ErrorsOfAsync(string? email)
    {
        ValidationResult result = await new ResendConfirmationCommandValidator()
            .ValidateAsync(new ResendConfirmationCommand(email), TestContext.Current.CancellationToken);
        return [.. result.Errors.Select(error => (error.PropertyName, error.ErrorMessage))];
    }

    [Fact]
    public async Task A_valid_email_has_no_errors()
    {
        Assert.Empty(await ErrorsOfAsync("ada@example.com"));
    }

    [Fact]
    public async Task A_missing_email_is_rejected_on_the_email_field()
    {
        Assert.Equal([("Email", "'Email' must not be empty.")], await ErrorsOfAsync(null));
    }

    [Fact]
    public async Task An_email_of_255_characters_is_rejected_on_the_email_field()
    {
        Assert.Equal(
            [("Email", "'Email' must be at most 254 characters.")],
            await ErrorsOfAsync(new string('a', 255 - "@example.com".Length) + "@example.com"));
    }
}
