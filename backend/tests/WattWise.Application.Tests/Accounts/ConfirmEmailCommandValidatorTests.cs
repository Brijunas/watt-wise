using FluentValidation.Results;

using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

public class ConfirmEmailCommandValidatorTests
{
    private static async Task<(string, string)[]> ErrorsOfAsync(string? id, string? token)
    {
        ValidationResult result = await new ConfirmEmailCommandValidator()
            .ValidateAsync(new ConfirmEmailCommand(id, token), TestContext.Current.CancellationToken);
        return [.. result.Errors.Select(error => (error.PropertyName, error.ErrorMessage))];
    }

    [Fact]
    public async Task A_guid_and_a_token_have_no_errors()
    {
        Assert.Empty(await ErrorsOfAsync("00000000-0000-0000-0000-0000000000c3", "tok"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task An_id_that_is_not_a_non_empty_guid_is_rejected(string? id)
    {
        Assert.Equal([("Id", "'Id' must be an id.")], await ErrorsOfAsync(id, "tok"));
    }

    [Fact]
    public async Task A_missing_token_is_rejected()
    {
        Assert.Equal([("Token", "'Token' must not be empty.")], await ErrorsOfAsync("00000000-0000-0000-0000-0000000000c3", null));
    }

    [Fact]
    public async Task An_empty_token_passes_validation_so_the_handler_rejects_it_as_a_bad_link()
    {
        Assert.Empty(await ErrorsOfAsync("00000000-0000-0000-0000-0000000000c3", ""));
    }
}
