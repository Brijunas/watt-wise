using FluentValidation.Results;

using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

public class RevokeAdminRoleCommandValidatorTests
{
    private static async Task<(string, string)[]> ErrorsOfAsync(string? email)
    {
        ValidationResult result = await new RevokeAdminRoleCommandValidator()
            .ValidateAsync(new RevokeAdminRoleCommand(email), TestContext.Current.CancellationToken);
        return [.. result.Errors.Select(error => (error.PropertyName, error.ErrorMessage))];
    }

    [Fact]
    public async Task An_email_has_no_errors()
    {
        Assert.Empty(await ErrorsOfAsync("ada@example.com"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_missing_or_empty_email_is_rejected_on_the_email_field(string? email)
    {
        Assert.Equal([("Email", "'Email' must not be empty.")], await ErrorsOfAsync(email));
    }
}
