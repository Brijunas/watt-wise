using Microsoft.Extensions.Options;

namespace WattWise.Infrastructure.Email;

/// <summary>
/// Checks the retry settings of one kind (the options name is the kind). Messages name keys and never values.
/// </summary>
internal sealed class EmailRetrySettingsValidator : IValidateOptions<EmailRetrySettings>
{
    public ValidateOptionsResult Validate(string? name, EmailRetrySettings options)
    {
        string section = $"{EmailRetrySettings.SectionName}:{name ?? "<Kind>"}";
        List<string> failures = [];

        if (options.Attempts < 1)
        {
            failures.Add($"{section}:Attempts must be at least one.");
        }

        if (options.Delays.Count != options.Attempts)
        {
            failures.Add($"{section}:Delays must have one entry per attempt.");
        }
        else if (options.Delays.Any(delay => delay < TimeSpan.Zero))
        {
            failures.Add($"{section}:Delays must be valid and not negative.");
        }
        else if (options.Delays.Count > 0 && options.Delays[0] != TimeSpan.Zero)
        {
            failures.Add($"{section}:Delays must start with zero.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
