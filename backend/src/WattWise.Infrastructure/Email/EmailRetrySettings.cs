using Microsoft.Extensions.Configuration;

using WattWise.Application.Email;

namespace WattWise.Infrastructure.Email;

/// <summary>The retry settings of one email kind, read from <c>Email:Retry:&lt;Kind&gt;</c>: how many attempts and the delay before each.</summary>
public sealed class EmailRetrySettings
{
    public const string SectionName = "Email:Retry";

    public int Attempts { get; set; }

    public IReadOnlyList<TimeSpan> Delays { get; set; } = [];

    /// <summary>The section of one kind, for example <c>Email:Retry:PasswordReset</c>.</summary>
    public static string SectionFor(EmailRequestKind kind) => $"{SectionName}:{kind}";

    /// <summary>Fills this instance from the kind's section; missing values stay 0 and empty.</summary>
    public void ReadFrom(IConfiguration configuration, EmailRequestKind kind)
    {
        IConfigurationSection section = configuration.GetSection(SectionFor(kind));
        Attempts = int.TryParse(section["Attempts"], out int attempts) ? attempts : 0;
        Delays = [.. section.GetSection("Delays").GetChildren()
            .Select(child => TimeSpan.TryParse(child.Value, System.Globalization.CultureInfo.InvariantCulture, out TimeSpan delay) ? delay : TimeSpan.MinValue)];
    }
}
