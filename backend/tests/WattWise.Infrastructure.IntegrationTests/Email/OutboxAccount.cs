namespace WattWise.Infrastructure.IntegrationTests.Email;

/// <summary>An account seeded for an outbox test: its ids and its (normalized) email address.</summary>
internal sealed record OutboxAccount(Guid AccountId, Guid IdentityId, string Email);
