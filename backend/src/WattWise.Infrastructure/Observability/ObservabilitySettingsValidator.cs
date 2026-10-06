using Microsoft.Extensions.Options;

namespace WattWise.Infrastructure.Observability;

/// <summary>
/// Fails options resolution (and host start, through ValidateOnStart) when the OTLP endpoint is set
/// but not an http or https origin. A blank endpoint is valid: the exporters are off. The endpoint
/// is not secret, so the value is shown.
/// </summary>
internal sealed class ObservabilitySettingsValidator : IValidateOptions<ObservabilitySettings>
{
    public ValidateOptionsResult Validate(string? name, ObservabilitySettings options)
    {
        if (string.IsNullOrWhiteSpace(options.OtlpEndpoint) || options.ValidOtlpEndpoint() is not null)
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"Observability configuration is invalid: {ObservabilitySettings.SectionName}:{nameof(ObservabilitySettings.OtlpEndpoint)} "
            + $"('{options.OtlpEndpoint}') must be an absolute http or https origin without path, query, fragment or user info.");
    }
}
