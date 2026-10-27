using System.Text;

namespace WattWise.Infrastructure.Tests.Accounts;

public class PasswordRuleTests
{
    private const string Ligature = "ﬃ"; // "ffi" once NFKC-normalized

    [Theory]
    [InlineData("Abcdefgh-123x", "PasswordTooShort")] // 13 characters, every class present
    [InlineData("ABCDEFGH-1234567", "PasswordRequiresLower")]
    [InlineData("abcdefgh-1234567", "PasswordRequiresUpper")]
    [InlineData("Abcdefgh-ijklmnop", "PasswordRequiresDigit")]
    [InlineData("Abcdefgh1234567x", "PasswordRequiresNonAlphanumeric")]
    public async Task A_password_that_breaks_one_rule_gets_exactly_that_error(string password, string expectedCode)
    {
        IReadOnlyList<string> codes = await PasswordRuleResults.ErrorCodesAsync(password);

        Assert.Equal([expectedCode], codes);
    }

    [Fact]
    public async Task A_password_of_257_characters_gets_exactly_the_too_long_error()
    {
        string password = "Aa1-" + new string('x', 253); // 4 + 253 = 257

        IReadOnlyList<string> codes = await PasswordRuleResults.ErrorCodesAsync(password);

        Assert.Equal(["PasswordTooLong"], codes);
    }

    public static TheoryData<string> AcceptedPasswords => new()
    {
        "Abcdefgh-1234x", // 14 characters
        "Aa1-" + new string('x', 252), // 4 + 252 = 256 characters
    };

    [Theory]
    [MemberData(nameof(AcceptedPasswords))]
    public async Task A_password_of_14_to_256_characters_with_every_class_is_accepted(string password)
    {
        IReadOnlyList<string> codes = await PasswordRuleResults.ErrorCodesAsync(password);

        Assert.Empty(codes);
    }

    // characterization: NormalizingPasswordValidator.cs:17 (the rules see the NFKC form)
    [Fact]
    public async Task A_password_of_12_characters_that_is_14_once_normalized_is_accepted()
    {
        string password = "Aa1-" + Ligature + new string('x', 7); // 4 + 1 + 7 = 12 typed; 4 + 3 + 7 = 14 after NFKC
        Assert.Equal((12, 14), (password.Length, password.Normalize(NormalizationForm.FormKC).Length));

        IReadOnlyList<string> codes = await PasswordRuleResults.ErrorCodesAsync(password);

        Assert.Empty(codes);
    }

    // characterization: PasswordMaxLengthValidator.cs:14 (the limit counts the NFKC form)
    [Fact]
    public async Task A_password_of_256_characters_that_is_258_once_normalized_gets_exactly_the_too_long_error()
    {
        string password = "Aa1-" + Ligature + new string('x', 251); // 4 + 1 + 251 = 256 typed; 4 + 3 + 251 = 258 after NFKC
        Assert.Equal((256, 258), (password.Length, password.Normalize(NormalizationForm.FormKC).Length));

        IReadOnlyList<string> codes = await PasswordRuleResults.ErrorCodesAsync(password);

        Assert.Equal(["PasswordTooLong"], codes);
    }
}
