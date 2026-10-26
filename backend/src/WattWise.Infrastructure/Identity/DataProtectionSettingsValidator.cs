using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Options;

namespace WattWise.Infrastructure.Identity;

/// <summary>Fails options resolution (and host start, through ValidateOnStart) when the certificate is missing or unusable; never prints it.</summary>
internal sealed class DataProtectionSettingsValidator : IValidateOptions<DataProtectionSettings>
{
    public ValidateOptionsResult Validate(string? name, DataProtectionSettings options)
    {
        string key = $"{DataProtectionSettings.SectionName}:{nameof(DataProtectionSettings.Certificate)}";
        if (string.IsNullOrWhiteSpace(options.Certificate))
        {
            return ValidateOptionsResult.Fail($"Data Protection configuration is missing or invalid: {key}.");
        }

        try
        {
            using X509Certificate2 certificate = X509Certificate2.CreateFromPem(options.Certificate, options.Certificate);
            using RSA? privateKey = certificate.GetRSAPrivateKey();
            return privateKey is null
                ? ValidateOptionsResult.Fail($"Data Protection configuration is invalid: {key} must contain a certificate with an RSA private key.")
                : ValidateOptionsResult.Success;
        }
        catch (CryptographicException)
        {
            return ValidateOptionsResult.Fail($"Data Protection configuration is invalid: {key} must be a PEM certificate followed by its private key.");
        }
    }
}
