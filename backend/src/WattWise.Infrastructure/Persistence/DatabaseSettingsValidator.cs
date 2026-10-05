using Microsoft.Extensions.Options;

namespace WattWise.Infrastructure.Persistence;

/// <summary>Fails options resolution (and host start, through ValidateOnStart) listing the missing keys, never values.</summary>
internal sealed class DatabaseSettingsValidator : IValidateOptions<DatabaseSettings>
{
    public ValidateOptionsResult Validate(string? name, DatabaseSettings options)
    {
        IReadOnlyList<string> missing = options.MissingKeys();
        return missing.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"Database configuration is missing or invalid: {string.Join(", ", missing)}.");
    }
}
