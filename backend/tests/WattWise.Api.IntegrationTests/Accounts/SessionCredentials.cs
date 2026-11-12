namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>An account made directly in the database, with the email and password to sign in to it.</summary>
public sealed record SessionCredentials(AccountSeeder.Ids Ids, string Email, string Password);
