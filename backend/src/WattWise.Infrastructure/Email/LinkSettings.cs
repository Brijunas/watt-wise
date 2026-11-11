using Microsoft.Extensions.Configuration;

namespace WattWise.Infrastructure.Email;

/// <summary>Where the links in emails point, read from the "Links" section.</summary>
public sealed class LinkSettings
{
    public const string SectionName = "Links";

    /// <summary>The frontend's absolute http or https origin, without a path or trailing slash.</summary>
    public string? FrontendBaseUrl { get; set; }

    /// <summary>Fills this instance from the section.</summary>
    public void ReadFrom(IConfiguration configuration)
    {
        FrontendBaseUrl = configuration.GetSection(SectionName)["FrontendBaseUrl"];
    }
}
