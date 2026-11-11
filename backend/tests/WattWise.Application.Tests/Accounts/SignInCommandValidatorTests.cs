using FluentValidation.Results;

using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

public class SignInCommandValidatorTests
{
    private static async Task<(string, string)[]> ErrorsOfAsync(string? email, string? password)
    {
        ValidationResult result = await new SignInCommandValidator()
            .ValidateAsync(new SignInCommand(email, password, null), TestContext.Current.CancellationToken);
        return [.. result.Errors.Select(error => (error.PropertyName, error.ErrorMessage))];
    }

    [Fact]
    public async Task An_email_and_a_password_have_no_errors_whatever_the_password_looks_like()
    {
        Assert.Empty(await ErrorsOfAsync("ada@example.com", "a"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_missing_or_empty_email_is_rejected(string? email)
    {
        Assert.Equal([("Email", "'Email' must not be empty.")], await ErrorsOfAsync(email, "pw"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_missing_or_empty_password_is_rejected(string? password)
    {
        Assert.Equal([("Password", "'Password' must not be empty.")], await ErrorsOfAsync("ada@example.com", password));
    }

    [Fact]
    public async Task A_password_of_256_characters_is_accepted()
    {
        Assert.Empty(await ErrorsOfAsync("ada@example.com", new string('a', 256)));
    }

    [Fact]
    public async Task A_password_of_257_characters_is_rejected()
    {
        Assert.Equal(
            [("Password", "The length of 'Password' must be 256 characters or fewer. You entered 257 characters.")],
            await ErrorsOfAsync("ada@example.com", new string('a', 257)));
    }

    [Fact]
    public async Task An_email_of_254_characters_is_accepted()
    {
        Assert.Empty(await ErrorsOfAsync(new string('a', 254), "pw"));
    }

    [Fact]
    public async Task An_email_of_255_characters_is_rejected()
    {
        Assert.Equal(
            [("Email", "The length of 'Email' must be 254 characters or fewer. You entered 255 characters.")],
            await ErrorsOfAsync(new string('a', 255), "pw"));
    }
}
