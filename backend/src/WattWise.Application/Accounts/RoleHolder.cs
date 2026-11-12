namespace WattWise.Application.Accounts;

/// <summary>The account a role is granted to or taken from.</summary>
/// <param name="AccountId">The account's id.</param>
/// <param name="EmailConfirmed">Whether the account's email address is confirmed.</param>
public sealed record RoleHolder(Guid AccountId, bool EmailConfirmed);
