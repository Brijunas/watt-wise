using System.Text;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.Tests.Accounts;

public class NormalizingPasswordHasherTests
{
    private static readonly AccountIdentity Identity = AccountIdentity.Create(Guid.NewGuid(), "someone@example.lt");

    private readonly NormalizingPasswordHasher _hasher = new(Options.Create(new PasswordHasherOptions()));

    [Fact]
    public void A_password_hashed_in_nfc_verifies_in_nfd()
    {
        const string nfc = "Pässwörd-Ünïcode-1";
        string nfd = nfc.Normalize(NormalizationForm.FormD);
        Assert.NotEqual(nfc, nfd);
        string hash = _hasher.HashPassword(Identity, nfc);

        PasswordVerificationResult result = _hasher.VerifyHashedPassword(Identity, hash, nfd);

        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void A_different_password_does_not_verify()
    {
        string hash = _hasher.HashPassword(Identity, "Pässwörd-Ünïcode-1");

        PasswordVerificationResult result = _hasher.VerifyHashedPassword(Identity, hash, "Pässwörd-Ünïcode-1x");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void A_password_hashed_with_compatibility_characters_verifies_in_its_plain_form()
    {
        string hash = _hasher.HashPassword(Identity, "Ａbc-ﬃ-123-Xyz"); // full-width A, ffi ligature

        PasswordVerificationResult result = _hasher.VerifyHashedPassword(Identity, hash, "Abc-ffi-123-Xyz");

        Assert.Equal(PasswordVerificationResult.Success, result);
    }
}
