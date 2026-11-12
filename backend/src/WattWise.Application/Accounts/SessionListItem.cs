using NodaTime;

namespace WattWise.Application.Accounts;

/// <summary>A live session as the query reads it, before the caller's own session is marked.</summary>
/// <param name="Id">The session's id.</param>
/// <param name="DeviceLabel">The label made from the user agent at sign-in.</param>
/// <param name="SignedInAt">When the session started.</param>
/// <param name="LastUsedAt">When the session last refreshed.</param>
public sealed record SessionListItem(Guid Id, string DeviceLabel, Instant SignedInAt, Instant LastUsedAt);
