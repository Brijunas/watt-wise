using FluentValidation.Results;

using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

public class ResetPasswordCommandValidatorTests
{
    private const string ValidId = "00000000-0000-0000-0000-0000000000c3";

    private static async Task<(string, string)[]> ErrorsOfAsync(string? id, string? token, string? password, params string[] policyMessages)
    {
        ValidationResult result = await new ResetPasswordCommandValidator(new FixedPasswordPolicy([], policyMessages))
            .ValidateAsync(new ResetPasswordCommand(id, token, password), TestContext.Current.CancellationToken);
        return [.. result.Errors.Select(error => (error.PropertyName, error.ErrorMessage))];
    }

    [Fact]
    public async Task A_complete_command_has_no_errors()
    {
        Assert.Empty(await ErrorsOfAsync(ValidId, "tok", "Aa1!secret"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task An_id_that_is_not_a_non_empty_guid_is_rejected(string? id)
    {
        Assert.Equal([("Id", "'Id' must be an id.")], await ErrorsOfAsync(id, "tok", "Aa1!secret"));
    }

    [Fact]
    public async Task A_missing_token_is_rejected()
    {
        Assert.Equal([("Token", "'Token' must not be empty.")], await ErrorsOfAsync(ValidId, null, "Aa1!secret"));
    }

    [Fact]
    public async Task An_empty_token_passes_validation_so_the_handler_rejects_it_as_a_bad_link()
    {
        Assert.Empty(await ErrorsOfAsync(ValidId, string.Empty, "Aa1!secret"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_missing_new_password_is_rejected_without_asking_the_policy(string? password)
    {
        Assert.Equal(
            [("NewPassword", "'New Password' must not be empty.")],
            await ErrorsOfAsync(ValidId, "tok", password, "policy was asked"));
    }

    [Fact]
    public async Task Each_message_of_the_password_policy_becomes_an_error_on_the_new_password()
    {
        Assert.Equal(
            [("NewPassword", "too short"), ("NewPassword", "no symbol")],
            await ErrorsOfAsync(ValidId, "tok", "x", "too short", "no symbol"));
    }
}
