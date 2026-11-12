namespace WattWise.Api.Cors;

/// <summary>CORS settings, read from the "Cors" configuration section.</summary>
public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>Origins allowed to call the API from a browser. Empty is valid and means no cross-origin access.</summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>
    /// The admin app's origin, one of <see cref="AllowedOrigins"/>. Its requests use their own refresh cookie. Blank or
    /// null means there is no admin app: every request uses the frontend's cookie.
    /// </summary>
    public string? AdminOrigin { get; set; }
}
