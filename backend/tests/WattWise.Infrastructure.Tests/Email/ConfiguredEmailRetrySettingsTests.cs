using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using WattWise.Application.Email;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.Tests.Email;

public class ConfiguredEmailRetrySettingsTests
{
    [Fact]
    public void Each_kind_gets_the_schedule_of_its_own_named_options()
    {
        ServiceCollection services = new();
        services.AddOptions<EmailRetrySettings>("PasswordReset").Configure(settings =>
        {
            settings.Attempts = 4;
            settings.Delays = [TimeSpan.Zero, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];
        });
        services.AddOptions<EmailRetrySettings>("AccountAlreadyExists").Configure(settings =>
        {
            settings.Attempts = 3;
            settings.Delays = [TimeSpan.Zero, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30)];
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        ConfiguredEmailRetrySettings adapter = new(provider.GetRequiredService<IOptionsMonitor<EmailRetrySettings>>());

        EmailRetrySchedule reset = adapter.For(EmailRequestKind.PasswordReset);
        EmailRetrySchedule exists = adapter.For(EmailRequestKind.AccountAlreadyExists);

        Assert.Equal(
            ("4:0,1,5,15", "3:0,5,30"),
            ($"{reset.Attempts}:{string.Join(",", reset.Delays.Select(d => d.TotalMinutes))}", $"{exists.Attempts}:{string.Join(",", exists.Delays.Select(d => d.TotalMinutes))}"));
    }
}
