using NodaTime;

namespace WattWise.Application.Accounts;

/// <summary>One of the account's live sessions (a device it is signed in on).</summary>
/// <param name="Id">The session's id.</param>
/// <param name="DeviceLabel">The label made from the user agent at sign-in.</param>
/// <param name="SignedInAt">When the session started.</param>
/// <param name="LastUsedAt">When the session last refreshed.</param>
/// <param name="IsCurrent">Whether this is the session of the access token that asked.</param>
public sealed record SessionSummary(Guid Id, string DeviceLabel, Instant SignedInAt, Instant LastUsedAt, bool IsCurrent);
