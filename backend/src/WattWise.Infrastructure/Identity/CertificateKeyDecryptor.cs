using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Xml.Linq;

using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;

namespace WattWise.Infrastructure.Identity;

/// <summary>Decrypts Data Protection keys that <see cref="CertificateKeyEncryptor"/> encrypted. The Data Protection key ring creates it, and passes its service provider.</summary>
internal sealed class CertificateKeyDecryptor(IServiceProvider services) : IXmlDecryptor
{
    private readonly DataProtectionCertificate _certificate = services.GetRequiredService<DataProtectionCertificate>();

    public XElement Decrypt(XElement encryptedElement)
    {
        XmlDocument document = new();
        using (XmlReader reader = encryptedElement.CreateReader())
        {
            document.Load(reader);
        }

        EncryptedData encryptedData = new();
        encryptedData.LoadXml(document.DocumentElement!);

        CertificateEncryptedXml encryptedXml = new(document, _certificate.Value);
        byte[]? key = encryptedXml.GetDecryptionKey(encryptedData, null) is { } symmetric
            ? encryptedXml.DecryptData(encryptedData, symmetric)
            : null;
        if (key is null)
        {
            throw new CryptographicException("The key could not be decrypted with the configured certificate.");
        }

        using MemoryStream stream = new(key);
        return XElement.Load(stream);
    }

    private sealed class CertificateEncryptedXml(XmlDocument document, X509Certificate2 certificate) : EncryptedXml(document)
    {
        public override byte[]? DecryptEncryptedKey(EncryptedKey encryptedKey)
        {
            using RSA rsa = certificate.GetRSAPrivateKey()
                ?? throw new CryptographicException("The Data Protection certificate has no RSA private key.");
            bool oaep = encryptedKey.EncryptionMethod?.KeyAlgorithm == XmlEncRSAOAEPUrl;
            return DecryptKey(encryptedKey.CipherData.CipherValue!, rsa, oaep);
        }
    }
}
