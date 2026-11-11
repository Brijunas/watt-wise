using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.Accounts;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class IdentityPasswordPolicyTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private async Task<IReadOnlyList<string>> CheckAsync(string password)
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPasswordPolicy>().CheckAsync(password, AccountsTestSupport.Token);
    }

    [Fact]
    public async Task A_valid_password_gives_no_messages()
    {
        Assert.Empty(await CheckAsync(AccountsTestSupport.ValidPassword()));
    }

    [Fact]
    public async Task Every_broken_rule_gives_a_message()
    {
        // "a": too short, no symbol, no digit, no uppercase (ASP.NET Core Identity's default descriptions).
        Assert.Equal(
            [
                "Passwords must be at least 14 characters.",
                "Passwords must have at least one non alphanumeric character.",
                "Passwords must have at least one digit ('0'-'9').",
                "Passwords must have at least one uppercase ('A'-'Z').",
            ],
            await CheckAsync("a"));
    }

    [Fact]
    public async Task A_password_over_256_characters_gives_the_too_long_message()
    {
        Assert.Equal(["Passwords must be at most 256 characters."], await CheckAsync("Aa1!" + new string('a', 253)));
    }
}
