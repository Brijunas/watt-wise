namespace WattWise.Application.Accounts;

/// <summary>Makes refresh tokens and hashes presented ones; only hashes are stored.</summary>
public interface IRefreshTokenFactory
{
    NewRefreshToken Create();

    byte[] Hash(string token);
}
