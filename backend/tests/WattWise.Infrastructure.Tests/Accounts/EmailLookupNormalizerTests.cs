using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.Tests.Accounts;

public class EmailLookupNormalizerTests
{
    private readonly EmailLookupNormalizer _normalizer = new();

    [Theory]
    [InlineData("  Ｊ＠ﬁ.LT ", "j@fi.lt")]
    [InlineData(null, null)]
    public void NormalizeEmail_gives_the_stored_form_and_keeps_null(string? email, string? expected)
    {
        string? result = _normalizer.NormalizeEmail(email);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("  Ｊ＠ﬁ.LT ", "j@fi.lt")]
    [InlineData(null, null)]
    public void NormalizeName_gives_the_stored_form_and_keeps_null(string? name, string? expected)
    {
        string? result = _normalizer.NormalizeName(name);

        Assert.Equal(expected, result);
    }
}
