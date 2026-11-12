namespace WattWise.Api.Endpoints;

/// <summary>One device the account is signed in on.</summary>
/// <param name="Id">The session's id.</param>
/// <param name="DeviceLabel">The label made from the user agent at sign-in.</param>
/// <param name="SignedInAt">When the session started, as an ISO 8601 instant.</param>
/// <param name="LastUsedAt">When the session last refreshed, as an ISO 8601 instant.</param>
/// <param name="Current">Whether this is the session of the access token that asked.</param>
public sealed record SessionResponse(Guid Id, string DeviceLabel, DateTimeOffset SignedInAt, DateTimeOffset LastUsedAt, bool Current);
