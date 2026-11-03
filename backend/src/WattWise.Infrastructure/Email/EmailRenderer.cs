using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Unicode;

using Fluid;

using Mjml.Net;

using WattWise.Application.Accounts;
using WattWise.Application.Email;
using WattWise.Domain.Localization;

namespace WattWise.Infrastructure.Email;

/// <summary>
/// Turns an <see cref="EmailMessage"/> into a subject, a text part and an HTML part. Fluid fills the
/// embedded templates in <c>Templates/&lt;Kind&gt;/&lt;lang&gt;.{subject,text,mjml}.liquid</c>, the MJML part is wrapped
/// in <c>_Layout.mjml.liquid</c> and compiled to email-safe HTML. A broken template throws, so tests catch it, not users.
/// </summary>
internal sealed class EmailRenderer
{
    private const string ResourcePrefix = "WattWise.Infrastructure.Email.Templates.";

    private static readonly FluidParser Parser = new();

    private static readonly MjmlRenderer Mjml = new();

    // Escapes < > & ' " but keeps Lithuanian letters readable, unlike Html (Latin only).
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    // Parsed once; templates are immutable, so the cache is safe to share between threads.
    private readonly Dictionary<string, IFluidTemplate> _templates = LoadTemplates();

    public async Task<RenderedEmail> RenderAsync(EmailMessage email, CancellationToken cancellationToken)
    {
        (string kind, Dictionary<string, string> model) = email switch
        {
            EmailConfirmationEmail confirmation => ("EmailConfirmation", new Dictionary<string, string> { ["link"] = confirmation.Link.AbsoluteUri }),
            AccountAlreadyExistsEmail => ("AccountAlreadyExists", new Dictionary<string, string>()),
            PasswordResetEmail reset => ("PasswordReset", new Dictionary<string, string> { ["link"] = reset.Link.AbsoluteUri }),
            _ => throw new InvalidOperationException($"No email template for {email.GetType().Name}."),
        };

        // Fluid and the MJML compile run in memory; this is the last point to stop before them.
        cancellationToken.ThrowIfCancellationRequested();

        string lang = LanguageFolder(email.Language);
        model["lang"] = lang;
        model["footer"] = Footer(email.Language);

        string subject = (await Render($"{kind}.{lang}.subject", model, NullEncoder.Default)).Trim();
        string text = await Render($"{kind}.{lang}.text", model, NullEncoder.Default);

        // The body is encoded here, so the layout inserts it as it is.
        string body = await Render($"{kind}.{lang}.mjml", model, Html);
        model["body"] = body;
        string mjml = await Render("_Layout.mjml", model, Html);

        var result = await Mjml.RenderAsync(mjml, ct: cancellationToken);
        if (result.Errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"MJML for {kind} ({lang}) has errors: {string.Join("; ", result.Errors.Select(error => error.Error))}");
        }

        return new RenderedEmail(subject, text, result.Html);
    }

    private async Task<string> Render(string name, Dictionary<string, string> model, TextEncoder encoder)
    {
        TemplateContext context = new();
        foreach ((string key, string value) in model)
        {
            context.SetValue(key, value);
        }

        return await _templates[name].RenderAsync(context, encoder);
    }

    private static string LanguageFolder(Language language) => language switch
    {
        Language.Lt => "lt",
        Language.En => "en",
    };

    private static string Footer(Language language) => language switch
    {
        Language.Lt => "Tai automatinis laiškas.",
        Language.En => "This is an automated message.",
    };

    private static Dictionary<string, IFluidTemplate> LoadTemplates()
    {
        Assembly assembly = typeof(EmailRenderer).Assembly;
        Dictionary<string, IFluidTemplate> templates = [];
        foreach (string resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            using Stream stream = assembly.GetManifestResourceStream(resource)!;
            using StreamReader reader = new(stream);
            string name = resource[ResourcePrefix.Length..^".liquid".Length];
            templates[name] = Parser.Parse(reader.ReadToEnd());
        }

        return templates;
    }
}
