using Mediator;

using WattWise.Application.Results;
using WattWise.Domain.Accounts;

namespace WattWise.Application.Accounts;

public sealed class GrantAdminRoleCommandHandler(IEmailAddressNormalizer normalizer, IAccountRoles roles)
    : ICommandHandler<GrantAdminRoleCommand, Result<RoleChange>>
{
    public async ValueTask<Result<RoleChange>> Handle(GrantAdminRoleCommand command, CancellationToken cancellationToken)
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

        bool added = await roles.AddAsync(holder.AccountId, RoleNames.Admin, cancellationToken);
        return added ? RoleChange.Changed : RoleChange.Unchanged;
    }
}
