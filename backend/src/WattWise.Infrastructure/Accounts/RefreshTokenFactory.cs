using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using WattWise.Application.Accounts;

namespace WattWise.Infrastructure.Accounts;

/// <summary>A refresh token is 32 random bytes, Base64Url-encoded; only the SHA-256 hash of that text is stored.</summary>
internal sealed class RefreshTokenFactory : IRefreshTokenFactory
{
    private const int TokenByteCount = 32;

    public NewRefreshToken Create()
    {
        string token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenByteCount));
        return new NewRefreshToken(token, Hash(token));
    }

    public byte[] Hash(string token)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }
}
