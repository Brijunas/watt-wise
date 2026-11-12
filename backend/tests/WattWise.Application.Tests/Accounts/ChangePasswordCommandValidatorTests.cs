using FluentValidation.Results;

using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

public class ChangePasswordCommandValidatorTests
{
    private static async Task<(string, string)[]> ErrorsOfAsync(string? current, string? next, params string[] policyMessages)
    {
        ValidationResult result = await new ChangePasswordCommandValidator(new FixedPasswordPolicy([], policyMessages))
            .ValidateAsync(new ChangePasswordCommand(Guid.NewGuid(), current, next, "agent"), TestContext.Current.CancellationToken);
        return [.. result.Errors.Select(error => (error.PropertyName, error.ErrorMessage))];
    }

    [Fact]
    public async Task A_complete_command_has_no_errors()
    {
        Assert.Empty(await ErrorsOfAsync("old-pw", "Aa1!secret"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_missing_current_password_is_rejected_on_its_field(string? current)
    {
        Assert.Equal([("CurrentPassword", "'Current Password' must not be empty.")], await ErrorsOfAsync(current, "Aa1!secret"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_missing_new_password_is_rejected_without_asking_the_policy(string? next)
    {
        Assert.Equal(
            [("NewPassword", "'New Password' must not be empty.")],
            await ErrorsOfAsync("old-pw", next, "policy was asked"));
    }

    [Fact]
    public async Task Each_message_of_the_password_policy_becomes_an_error_on_the_new_password()
    {
        Assert.Equal(
            [("NewPassword", "too short"), ("NewPassword", "no symbol")],
            await ErrorsOfAsync("old-pw", "x", "too short", "no symbol"));
    }
}
