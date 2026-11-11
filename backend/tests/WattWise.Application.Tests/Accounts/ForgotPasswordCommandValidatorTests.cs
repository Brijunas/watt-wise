using FluentValidation.Results;

using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

public class ForgotPasswordCommandValidatorTests
{
    private static async Task<(string, string)[]> ErrorsOfAsync(string? email)
    {
        ValidationResult result = await new ForgotPasswordCommandValidator()
            .ValidateAsync(new ForgotPasswordCommand(email), TestContext.Current.CancellationToken);
        return [.. result.Errors.Select(error => (error.PropertyName, error.ErrorMessage))];
    }

    [Fact]
    public async Task A_valid_email_has_no_errors()
    {
        Assert.Empty(await ErrorsOfAsync("ada@example.com"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_missing_email_is_rejected_on_the_email_field(string? email)
    {
        Assert.Equal([("Email", "'Email' must not be empty.")], await ErrorsOfAsync(email));
    }

    [Fact]
    public async Task An_email_that_is_not_an_address_is_rejected_on_the_email_field()
    {
        Assert.Equal([("Email", "'Email' is not a valid email address.")], await ErrorsOfAsync("not-an-email"));
    }
}
