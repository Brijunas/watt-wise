using Mediator;

using WattWise.Application.Results;
using WattWise.Domain.Accounts;

namespace WattWise.Application.Accounts;

public sealed class RevokeAdminRoleCommandHandler(
    IEmailAddressNormalizer normalizer,
    IAccountRoles roles,
    IAccountSessions sessions,
    ITransactionRunner transactions) : ICommandHandler<RevokeAdminRoleCommand, Result<RoleChange>>
{
    public async ValueTask<Result<RoleChange>> Handle(RevokeAdminRoleCommand command, CancellationToken cancellationToken)
    {
        RoleHolder? holder = await roles.FindByEmailAsync(normalizer.Normalize(command.Email!), cancellationToken);
        if (holder is null)
        {
            return AccountErrors.AccountNotFound();
        }

        if (!holder.EmailConfirmed)
        {
            return AccountErrors.EmailNotConfirmed();
        }

        // The role and the sessions go together: no session may outlive the role by more than the token.
        return await transactions.ExecuteAsync<RoleChange>(
            async token =>
            {
                bool removed = await roles.RemoveAsync(holder.AccountId, RoleNames.Admin, token);
                if (!removed)
                {
                    return RoleChange.Unchanged;
                }

                _ = await sessions.EndAllAsync(holder.AccountId, token);
                return RoleChange.Changed;
            },
            cancellationToken);
    }
}
