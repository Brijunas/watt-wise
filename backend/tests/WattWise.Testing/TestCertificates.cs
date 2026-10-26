using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace WattWise.Testing;

/// <summary>Self-signed certificates for the data protection key ring, as PEM with the private key.</summary>
public static class TestCertificates
{
    private static readonly Lazy<string> Primary = new(() => CreatePem("WattWise Test A"));
    private static readonly Lazy<string> Other = new(() => CreatePem("WattWise Test B"));

    /// <summary>The certificate and its private key, one per process.</summary>
    public static string Pem => Primary.Value;

    /// <summary>A second, distinct certificate, for the case where the key ring was protected by another one.</summary>
    public static string OtherPem => Other.Value;

    /// <summary>Only the certificate, without its private key.</summary>
    public static string CertificateOnlyPem
    {
        get
        {
            using X509Certificate2 certificate = X509Certificate2.CreateFromPem(Pem);
            return certificate.ExportCertificatePem();
        }
    }

    private static string CreatePem(string subject)
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new($"CN={subject}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        return certificate.ExportCertificatePem() + "\n" + rsa.ExportPkcs8PrivateKeyPem();
    }
}
