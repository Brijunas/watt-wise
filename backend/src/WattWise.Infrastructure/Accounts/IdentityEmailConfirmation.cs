using Microsoft.AspNetCore.Identity;

using WattWise.Application.Accounts;

namespace WattWise.Infrastructure.Accounts;

internal sealed class IdentityEmailConfirmation(UserManager<AccountIdentity> users) : IEmailConfirmation
{
    public async Task<bool> ConfirmAsync(Guid accountIdentityId, string token, CancellationToken cancellationToken)
    {
        AccountIdentity? identity = await users.FindByIdAsync(accountIdentityId.ToString());
        if (identity is null)
        {
            return false;
        }

        // Nothing else may save in this scope after a failure: the token provider has marked the link used. See accounts.md.
        IdentityResult result = await users.ConfirmEmailAsync(identity, token);
        return result.Succeeded;
    }
}
