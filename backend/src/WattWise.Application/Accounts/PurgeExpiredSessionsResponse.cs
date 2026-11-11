namespace WattWise.Application.Accounts;

/// <summary>The number of sessions deleted.</summary>
public sealed record PurgeExpiredSessionsResponse(int Deleted);
