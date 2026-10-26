using Microsoft.Extensions.Configuration;

namespace WattWise.Infrastructure.Identity;

/// <summary>Data Protection settings, read from the "DataProtection" configuration section.</summary>
/// <remarks>A class rather than a record, so ToString never prints the certificate.</remarks>
public sealed class DataProtectionSettings
{
    public const string SectionName = "DataProtection";

    /// <summary>PEM text with the X.509 certificate and its private key, which encrypts the stored keys.</summary>
    public string? Certificate { get; set; }

    /// <summary>Fills this instance from the section.</summary>
    public void ReadFrom(IConfiguration configuration)
    {
        Certificate = configuration.GetSection(SectionName)["Certificate"];
    }
}
