namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>The ids of an account and of its AccountIdentity, as <see cref="AccountsTestSupport.CreateAccountIdentityAsync"/> returns them.</summary>
internal sealed record AccountIdentityIds(Guid AccountId, Guid IdentityId);
