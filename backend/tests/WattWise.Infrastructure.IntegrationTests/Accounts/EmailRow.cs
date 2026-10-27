namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>The four columns that hold an AccountIdentity's email, as stored.</summary>
internal sealed record EmailRow(string Email, string UserName, string NormalizedEmail, string NormalizedUserName);
