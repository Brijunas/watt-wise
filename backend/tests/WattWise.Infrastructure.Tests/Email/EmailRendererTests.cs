using CsCheck;

using WattWise.Application.Accounts;
using WattWise.Application.Email;
using WattWise.Domain.Localization;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.Tests.Email;

public class EmailRendererTests
{
    private static readonly Uri Link = new("https://app.wattwise.lt/confirm?token=a%2Fb&x=1");

    private readonly EmailRenderer _renderer = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<EmailMessage, string> SubjectsByKindAndLanguage => new()
    {
        { new EmailConfirmationEmail("a@example.test", Language.En, Link), "Confirm your email for Watt-Wise" },
        { new EmailConfirmationEmail("a@example.test", Language.Lt, Link), "Patvirtinkite el. paštą „Watt-Wise“" },
        { new PasswordResetEmail("a@example.test", Language.En, Link), "Reset your Watt-Wise password" },
        { new PasswordResetEmail("a@example.test", Language.Lt, Link), "Atkurkite „Watt-Wise“ slaptažodį" },
        { new AccountAlreadyExistsEmail("a@example.test", Language.En), "Someone tried to sign up to Watt-Wise with your email" },
        { new AccountAlreadyExistsEmail("a@example.test", Language.Lt), "Kažkas bandė registruotis „Watt-Wise“ naudodamas jūsų el. paštą" },
    };

    public static TheoryData<EmailMessage, string> TextFirstLinesByKindAndLanguage => new()
    {
        { new EmailConfirmationEmail("a@example.test", Language.En, Link), "Welcome to Watt-Wise!" },
        { new EmailConfirmationEmail("a@example.test", Language.Lt, Link), "Sveiki atvykę į „Watt-Wise“!" },
        { new PasswordResetEmail("a@example.test", Language.En, Link), "We received a request to reset the password of your Watt-Wise account." },
        { new PasswordResetEmail("a@example.test", Language.Lt, Link), "Gavome prašymą atkurti jūsų „Watt-Wise“ paskyros slaptažodį." },
        { new AccountAlreadyExistsEmail("a@example.test", Language.En), "Someone, maybe you, tried to sign up to Watt-Wise with this email address, but it already has an account." },
        { new AccountAlreadyExistsEmail("a@example.test", Language.Lt), "Kažkas, galbūt jūs, bandė registruotis „Watt-Wise“ naudodamas šį el. pašto adresą, tačiau jis jau turi paskyrą." },
    };

    public static TheoryData<EmailMessage> LinkEmails => new()
    {
        new EmailConfirmationEmail("a@example.test", Language.En, Link),
        new EmailConfirmationEmail("a@example.test", Language.Lt, Link),
        new PasswordResetEmail("a@example.test", Language.En, Link),
        new PasswordResetEmail("a@example.test", Language.Lt, Link),
    };

    public static TheoryData<EmailMessage, string> HtmlLanguageMarkers => new()
    {
        { new AccountAlreadyExistsEmail("a@example.test", Language.En), "<html lang=\"en\"" },
        { new AccountAlreadyExistsEmail("a@example.test", Language.Lt), "<html lang=\"lt\"" },
        { new EmailConfirmationEmail("a@example.test", Language.Lt, Link), "<html lang=\"lt\"" },
        { new PasswordResetEmail("a@example.test", Language.En, Link), "<html lang=\"en\"" },
    };

    public static TheoryData<EmailMessage, string> HtmlFooters => new()
    {
        { new AccountAlreadyExistsEmail("a@example.test", Language.En), "This is an automated message." },
        { new AccountAlreadyExistsEmail("a@example.test", Language.Lt), "Tai automatinis laiškas." },
        { new EmailConfirmationEmail("a@example.test", Language.Lt, Link), "Tai automatinis laiškas." },
        { new PasswordResetEmail("a@example.test", Language.En, Link), "This is an automated message." },
    };

    [Theory]
    [MemberData(nameof(SubjectsByKindAndLanguage))]
    public async Task Each_kind_and_language_gets_its_own_subject(EmailMessage email, string subject)
    {
        RenderedEmail rendered = await _renderer.RenderAsync(email, Token);

        Assert.Equal(subject, rendered.Subject);
    }

    [Theory]
    [MemberData(nameof(TextFirstLinesByKindAndLanguage))]
    public async Task Each_kind_and_language_gets_its_own_text_body(EmailMessage email, string firstLine)
    {
        RenderedEmail rendered = await _renderer.RenderAsync(email, Token);

        Assert.Equal(firstLine, rendered.Text.Split('\n')[0].TrimEnd('\r'));
    }

    [Theory]
    [MemberData(nameof(LinkEmails))]
    public async Task The_link_is_in_the_text_exactly_as_given(EmailMessage email)
    {
        RenderedEmail rendered = await _renderer.RenderAsync(email, Token);

        Assert.Contains("https://app.wattwise.lt/confirm?token=a%2Fb&x=1", rendered.Text, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(LinkEmails))]
    public async Task The_link_is_in_the_html_with_its_ampersand_encoded(EmailMessage email)
    {
        RenderedEmail rendered = await _renderer.RenderAsync(email, Token);

        Assert.Contains("https://app.wattwise.lt/confirm?token=a%2Fb&amp;x=1", rendered.Html, StringComparison.Ordinal);
    }

    // Uri.AbsoluteUri escapes < > and ", but leaves ' in a query, so the apostrophe is what reaches the renderer raw.
    [Fact]
    public async Task An_apostrophe_in_the_link_is_html_encoded_in_the_html()
    {
        EmailConfirmationEmail email = new("a@example.test", Language.En, new Uri("https://x.test/?name=o'neil"));

        RenderedEmail rendered = await _renderer.RenderAsync(email, Token);

        Assert.Contains("name=o&#x27;neil", rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_apostrophe_in_the_link_is_kept_in_the_text()
    {
        EmailConfirmationEmail email = new("a@example.test", Language.En, new Uri("https://x.test/?name=o'neil"));

        RenderedEmail rendered = await _renderer.RenderAsync(email, Token);

        Assert.Contains("name=o'neil", rendered.Text, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(HtmlLanguageMarkers))]
    public async Task The_html_document_declares_the_language(EmailMessage email, string tag)
    {
        RenderedEmail rendered = await _renderer.RenderAsync(email, Token);

        Assert.Contains(tag, rendered.Html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(HtmlFooters))]
    public async Task The_html_footer_follows_the_language(EmailMessage email, string footer)
    {
        RenderedEmail rendered = await _renderer.RenderAsync(email, Token);

        Assert.Contains(footer, rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_message_kind_without_a_template_throws_naming_the_type()
    {
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _renderer.RenderAsync(new UnknownEmail("a@example.test", Language.En), Token));

        Assert.Equal("No email template for UnknownEmail.", exception.Message);
    }

    [Fact]
    public async Task A_cancelled_token_stops_rendering()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _renderer.RenderAsync(new AccountAlreadyExistsEmail("a@example.test", Language.En), cancelled.Token));
    }

    // The query is built from & % ' and letters: the characters that differ between the text part (as given)
    // and the HTML part (& and ' escaped). The expected HTML is worked out by hand, not by an encoder.
    [Fact]
    public async Task Any_link_is_in_the_text_as_given_and_in_the_html_with_ampersands_and_apostrophes_escaped()
    {
        Gen<Uri> links = Gen.String[Gen.Char["&%'ab1-_="], 0, 20]
            .Select(query => new Uri($"https://app.wattwise.lt/c?q={query}"));

        await links.SampleAsync(
            async link =>
            {
                RenderedEmail rendered = await _renderer.RenderAsync(new PasswordResetEmail("a@example.test", Language.En, link), Token);
                string escaped = link.AbsoluteUri.Replace("&", "&amp;", StringComparison.Ordinal).Replace("'", "&#x27;", StringComparison.Ordinal);
                Assert.True(
                    rendered.Text.Contains(link.AbsoluteUri, StringComparison.Ordinal) && rendered.Html.Contains(escaped, StringComparison.Ordinal),
                    $"link {link.AbsoluteUri}");
            },
            iter: 50);
    }
}
