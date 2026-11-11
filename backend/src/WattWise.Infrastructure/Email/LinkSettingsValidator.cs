using Microsoft.Extensions.Options;

namespace WattWise.Infrastructure.Email;

/// <summary>Checks that the frontend's base URL is an absolute http or https origin. Messages name keys and never values.</summary>
internal sealed class LinkSettingsValidator : IValidateOptions<LinkSettings>
{
    private const string Key = $"{LinkSettings.SectionName}:{nameof(LinkSettings.FrontendBaseUrl)}";

    public ValidateOptionsResult Validate(string? name, LinkSettings options)
    {
        string? value = options.FrontendBaseUrl;
        bool valid = !string.IsNullOrWhiteSpace(value)
            && Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.Equals(value, uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);

        return valid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{Key} must be an absolute http or https URL without a path or a trailing slash.");
    }
}
