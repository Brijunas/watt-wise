using Microsoft.Extensions.Options;

using WattWise.Application.Email;

namespace WattWise.Infrastructure.Email;

/// <summary>Gives the Application layer the retry schedules read from <c>Email:Retry:&lt;Kind&gt;</c>.</summary>
internal sealed class ConfiguredEmailRetrySettings(IOptionsMonitor<EmailRetrySettings> options) : IEmailRetrySettings
{
    public EmailRetrySchedule For(EmailRequestKind kind)
    {
        EmailRetrySettings settings = options.Get(kind.ToString());
        return new EmailRetrySchedule(settings.Attempts, settings.Delays);
    }
}
