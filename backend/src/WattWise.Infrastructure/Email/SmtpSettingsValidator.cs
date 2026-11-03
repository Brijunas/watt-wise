using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace WattWise.Infrastructure.Email;

/// <summary>Fails options resolution (and host start, through ValidateOnStart) listing the missing keys, never values.</summary>
internal sealed class SmtpSettingsValidator(IHostEnvironment environment) : IValidateOptions<SmtpSettings>
{
    public ValidateOptionsResult Validate(string? name, SmtpSettings options)
    {
        IReadOnlyList<string> missing = options.MissingKeys(environment.IsDevelopment());
        return missing.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"Smtp configuration is missing or invalid: {string.Join(", ", missing)}.");
    }
}
