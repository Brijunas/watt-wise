namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>What a sign-in gave: the access token, the expiry text from the body and the refresh cookie as <c>name=value</c>.</summary>
public sealed record SignedInSession(string AccessToken, string AccessTokenExpiresAt, string Cookie);
