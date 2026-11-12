namespace WattWise.Application.Accounts;

/// <summary>What <see cref="ISmokeAccounts.EnsureAsync"/> found and did.</summary>
/// <param name="AccountId">The account's id.</param>
/// <param name="Outcome">Created, changed or unchanged by the call (roles are not part of it).</param>
/// <param name="PasswordChanged">Whether an existing account's password differed and was replaced.</param>
public sealed record EnsuredAccount(Guid AccountId, SeedOutcome Outcome, bool PasswordChanged);
