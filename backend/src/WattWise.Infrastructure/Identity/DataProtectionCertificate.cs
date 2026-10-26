using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Options;

namespace WattWise.Infrastructure.Identity;

/// <summary>Loads the validated certificate once, on first use, and keeps it for the life of the host.</summary>
internal sealed class DataProtectionCertificate(IOptions<DataProtectionSettings> settings) : IDisposable
{
    private readonly Lazy<X509Certificate2> _certificate = new(() =>
    {
        string pem = settings.Value.Certificate!;
        return X509Certificate2.CreateFromPem(pem, pem);
    });

    public X509Certificate2 Value => _certificate.Value;

    public void Dispose()
    {
        if (_certificate.IsValueCreated)
        {
            _certificate.Value.Dispose();
        }
    }
}
