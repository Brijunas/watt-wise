using Microsoft.AspNetCore.Identity;

using WattWise.Application.Accounts;

namespace WattWise.Infrastructure.Accounts;

/// <summary>Runs ASP.NET Core Identity's password validators (the NFKC-aware ones registered in <c>AddIdentity</c>) without creating anything.</summary>
internal sealed class IdentityPasswordPolicy(UserManager<AccountIdentity> users) : IPasswordPolicy
{
    public async Task<IReadOnlyList<string>> CheckAsync(string password, CancellationToken cancellationToken)
    {
        List<string> messages = [];
        AccountIdentity probe = new();
        foreach (IPasswordValidator<AccountIdentity> validator in users.PasswordValidators)
        {
            IdentityResult result = await validator.ValidateAsync(users, probe, password);
            messages.AddRange(result.Errors.Select(error => error.Description));
        }

        return messages;
    }
}
