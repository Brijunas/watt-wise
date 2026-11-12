using System.Security.Claims;

namespace WattWise.Api.Endpoints;

/// <summary>Reads the ids an access token carries. The claims keep their token names (MapInboundClaims is off).</summary>
internal static class AccessTokenClaims
{
    /// <summary>The account id, from <c>sub</c>. Bearer validation guarantees a GUID on any token this Api issued, so a <c>false</c> is defensive only.</summary>
    public static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId)
    {
        return Guid.TryParse(user.FindFirstValue("sub"), out accountId);
    }

    /// <summary>The session id, from <c>sid</c>; <c>null</c> when the claim is missing or not a GUID.</summary>
    public static Guid? GetSessionId(ClaimsPrincipal user)
    {
        return Guid.TryParse(user.FindFirstValue("sid"), out Guid sessionId) ? sessionId : null;
    }
}
