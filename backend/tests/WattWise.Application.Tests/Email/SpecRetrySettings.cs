using WattWise.Application.Email;

namespace WattWise.Application.Tests.Email;

/// <summary>The retry schedules of the spec (email-outbox.md), written out as literals, not read from production.</summary>
internal sealed class SpecRetrySettings : IEmailRetrySettings
{
    public EmailRetrySchedule For(EmailRequestKind kind) => kind switch
    {
        EmailRequestKind.PasswordReset => new EmailRetrySchedule(4, Minutes(0, 1, 5, 15)),
        EmailRequestKind.EmailConfirmation => new EmailRetrySchedule(6, Minutes(0, 1, 5, 15, 60, 240)),
        _ => new EmailRetrySchedule(3, Minutes(0, 5, 30)),
    };

    private static List<TimeSpan> Minutes(params int[] minutes) => [.. minutes.Select(m => TimeSpan.FromMinutes(m))];
}
