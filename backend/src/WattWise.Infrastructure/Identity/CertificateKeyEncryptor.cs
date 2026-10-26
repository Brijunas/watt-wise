using System.Xml.Linq;

using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace WattWise.Infrastructure.Identity;

/// <summary>
/// Encrypts Data Protection keys with the certificate, as the framework's CertificateXmlEncryptor
/// does, but names <see cref="CertificateKeyDecryptor"/> as the way back. The framework's own
/// decryptor reads certificates from an options type it keeps internal, so a certificate that is
/// only known after the host is built can't reach it.
/// </summary>
internal sealed class CertificateKeyEncryptor(IXmlEncryptor inner) : IXmlEncryptor
{
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        EncryptedXmlInfo encrypted = inner.Encrypt(plaintextElement);
        return new EncryptedXmlInfo(encrypted.EncryptedElement, typeof(CertificateKeyDecryptor));
    }
}
