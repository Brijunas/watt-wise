using Mediator;

using WattWise.Application.Results;
using WattWise.Domain.Accounts;

namespace WattWise.Application.Accounts;

public sealed class SeedSmokeAccountCommandHandler(
    IEmailAddressNormalizer normalizer,
    ISmokeAccounts accounts,
    IAccountRoles roles,
    IAccountSessions sessions,
    ITransactionRunner transactions) : ICommandHandler<SeedSmokeAccountCommand, Result<SeedOutcome>>
{
    public async ValueTask<Result<SeedOutcome>> Handle(SeedSmokeAccountCommand command, CancellationToken cancellationToken)
    {
        string email = normalizer.Normalize(command.Email!);

        // The account, its role and its sessions are one unit, so a failure can't leave a new password with live sessions that
        // a rerun would never end: a changed password or a removed role ends every session.
        return await transactions.ExecuteAsync<SeedOutcome>(
            async token =>
            {
                EnsuredAccount ensured = await accounts.EnsureAsync(email, command.Password!, token);
                bool rolesChanged = command.Admin
                    ? await roles.AddAsync(ensured.AccountId, RoleNames.Admin, token)
                    : await roles.RemoveAsync(ensured.AccountId, RoleNames.Admin, token);

                if (ensured.PasswordChanged || (rolesChanged && !command.Admin))
                {
                    _ = await sessions.EndAllAsync(ensured.AccountId, token);
                }

                return ensured.Outcome == SeedOutcome.Unchanged && rolesChanged ? SeedOutcome.Changed : ensured.Outcome;
            },
            cancellationToken);
    }
}
