using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.Tests.Accounts;

/// <summary>Runs a password through every password validator the real <c>AddInfrastructure()</c> registers on <c>UserManager</c>; nothing connects to a database.</summary>
internal static class PasswordRuleResults
{
    public static async Task<IReadOnlyList<string>> ErrorCodesAsync(string password)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Host"] = "unused",
                ["Database:Port"] = "5432",
                ["Database:Name"] = "unused",
                ["Database:Username"] = "unused",
                ["Database:Password"] = "unused",
            })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddInfrastructure();
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity identity = AccountIdentity.Create(Guid.NewGuid(), "someone@example.lt");

        List<string> codes = [];
        foreach (IPasswordValidator<AccountIdentity> validator in users.PasswordValidators)
        {
            IdentityResult result = await validator.ValidateAsync(users, identity, password);
            codes.AddRange(result.Errors.Select(error => error.Code));
        }

        return codes;
    }
}
