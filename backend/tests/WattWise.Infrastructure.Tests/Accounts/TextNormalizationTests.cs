using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.Tests.Accounts;

public class TextNormalizationTests
{
    [Theory]
    [InlineData("  Ab@Example.LT ", "ab@example.lt")] // trim and lowercase
    [InlineData("ＡＢ＠Ｅｘａｍｐｌｅ．ＬＴ", "ab@example.lt")] // full-width forms
    [InlineData("ﬁnd@x.lt", "find@x.lt")] // fi ligature
    [InlineData("ÄB@X.LT", "äb@x.lt")] // non-ASCII lowercase
    public void Email_is_trimmed_nfkc_normalized_and_lowercase(string email, string expected)
    {
        string result = TextNormalization.Email(email);

        Assert.Equal(expected, result);
    }
}
