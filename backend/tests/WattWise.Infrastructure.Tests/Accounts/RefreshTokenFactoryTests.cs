using System.Security.Cryptography;
using System.Text;

using CsCheck;

using WattWise.Application.Accounts;
using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.Tests.Accounts;

public class RefreshTokenFactoryTests
{
    private readonly RefreshTokenFactory _factory = new();

    [Fact]
    public void A_new_token_is_43_Base64Url_characters_for_32_random_bytes()
    {
        NewRefreshToken token = _factory.Create();

        Assert.Matches("^[A-Za-z0-9_-]{43}$", token.Token);
    }

    [Fact]
    public void Two_new_tokens_differ()
    {
        Assert.NotEqual(_factory.Create().Token, _factory.Create().Token);
    }

    [Fact]
    public void The_hash_is_the_sha256_of_the_token_text()
    {
        // sha256("abc"), the well-known test vector.
        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", Convert.ToHexString(_factory.Hash("abc")));
    }

    [Fact]
    public void The_hash_of_a_created_token_is_the_one_returned_with_it()
    {
        Gen.Int[1, 20].Sample(
            _ =>
            {
                NewRefreshToken token = _factory.Create();
                return token.Hash.SequenceEqual(_factory.Hash(token.Token)) && token.Hash.Length == 32;
            },
            iter: 50);
    }

    [Fact]
    public void The_hash_is_over_the_utf8_bytes_of_the_text()
    {
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes("é")), _factory.Hash("é"));
    }
}
