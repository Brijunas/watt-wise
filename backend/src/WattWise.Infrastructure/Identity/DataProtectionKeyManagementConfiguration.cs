using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WattWise.Infrastructure.Identity;

/// <summary>Makes the Data Protection key ring encrypt new keys with the certificate. Runs when the key ring is first used, after configuration is final.</summary>
internal sealed class DataProtectionKeyManagementConfiguration(DataProtectionCertificate certificate, ILoggerFactory loggerFactory)
    : IConfigureOptions<KeyManagementOptions>
{
    public void Configure(KeyManagementOptions options)
    {
        options.XmlEncryptor = new CertificateKeyEncryptor(new CertificateXmlEncryptor(certificate.Value, loggerFactory));
    }
}
