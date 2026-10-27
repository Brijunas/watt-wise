using System.Text;

namespace WattWise.Infrastructure.Accounts;

internal static class TextNormalization
{
    /// <summary>NFKC-normalizes the text; text that isn't valid Unicode (a lone surrogate) is returned as it is.</summary>
    public static string Nfkc(string text)
    {
        try
        {
            return text.Normalize(NormalizationForm.FormKC);
        }
        catch (ArgumentException)
        {
            return text;
        }
    }

    /// <summary>The form an email is stored and looked up in: trimmed, NFKC-normalized, lowercase.</summary>
    public static string Email(string email)
    {
        return Nfkc(email.Trim()).ToLowerInvariant();
    }
}
