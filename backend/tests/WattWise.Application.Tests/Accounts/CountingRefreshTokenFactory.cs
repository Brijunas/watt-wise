using System.Text;

using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

/// <summary>Makes tokens <c>token-1</c>, <c>token-2</c>, … and hashes a token to its UTF-8 bytes, so a test can name the hash of any token.</summary>
internal sealed class CountingRefreshTokenFactory : IRefreshTokenFactory
{
    private int _count;

    public NewRefreshToken Create()
    {
        string token = $"token-{++_count}";
        return new NewRefreshToken(token, Hash(token));
    }

    public byte[] Hash(string token) => Encoding.UTF8.GetBytes(token);
}
