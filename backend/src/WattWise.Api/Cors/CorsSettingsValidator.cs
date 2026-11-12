using Microsoft.Extensions.Options;

namespace WattWise.Api.Cors;

/// <summary>
/// Fails options resolution (and host start, through ValidateOnStart) listing the invalid origins by
/// index. Origins are not secret, so the values are shown.
/// </summary>
internal sealed class CorsSettingsValidator : IValidateOptions<CorsSettings>
{
    public ValidateOptionsResult Validate(string? name, CorsSettings options)
    {
        List<string> invalid = [];
        for (int index = 0; index < options.AllowedOrigins.Length; index++)
        {
            string origin = options.AllowedOrigins[index];
            if (!IsValidOrigin(origin))
            {
                invalid.Add($"{CorsSettings.SectionName}:{nameof(CorsSettings.AllowedOrigins)}:{index} ('{origin}')");
            }
        }

        if (invalid.Count > 0)
        {
            return ValidateOptionsResult.Fail(
                $"Cors configuration has invalid origins (each must be an absolute http or https origin without path, query or fragment): {string.Join(", ", invalid)}.");
        }

        string? adminOrigin = options.AdminOrigin;
        if (string.IsNullOrWhiteSpace(adminOrigin))
        {
            return ValidateOptionsResult.Success;
        }

        if (!IsValidOrigin(adminOrigin))
        {
            return ValidateOptionsResult.Fail(
                $"{CorsSettings.SectionName}:{nameof(CorsSettings.AdminOrigin)} ('{adminOrigin}') must be an absolute http or https origin without path, query or fragment.");
        }

        return options.AllowedOrigins.Contains(adminOrigin, StringComparer.Ordinal)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{CorsSettings.SectionName}:{nameof(CorsSettings.AdminOrigin)} ('{adminOrigin}') must be one of {CorsSettings.SectionName}:{nameof(CorsSettings.AllowedOrigins)}.");
    }

    private static bool IsValidOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin) || origin.EndsWith('/'))
        {
            return false;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && uri.AbsolutePath == "/"
            && uri.Query.Length == 0
            && uri.Fragment.Length == 0
            && uri.UserInfo.Length == 0;
    }
}
