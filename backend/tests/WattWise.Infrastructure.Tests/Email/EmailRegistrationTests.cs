using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using WattWise.Application.Email;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.Tests.Email;

public class EmailRegistrationTests
{
    private static ServiceProvider Build(Dictionary<string, string?> smtp)
    {
        Dictionary<string, string?> settings = new(smtp)
        {
            ["Database:Host"] = "localhost",
            ["Database:Port"] = "5432",
            ["Database:Name"] = "n",
            ["Database:Username"] = "u",
            ["Database:Password"] = "p",
        };
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("Development"));
        services.AddLogging();
        services.AddInfrastructure();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Resolving_the_sender_without_smtp_settings_fails_naming_every_key()
    {
        using ServiceProvider provider = Build([]);

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IEmailSender>());

        Assert.Equal(
            "Smtp configuration is missing or invalid: Smtp:Host, Smtp:Port, Smtp:Security, Smtp:Username, Smtp:Password, Smtp:FromAddress, Smtp:FromName.",
            exception.Message);
    }

    [Fact]
    public void Resolving_the_sender_with_complete_smtp_settings_succeeds()
    {
        using ServiceProvider provider = Build(new()
        {
            ["Smtp:Host"] = "smtp.example.test",
            ["Smtp:Port"] = "587",
            ["Smtp:Security"] = "StartTls",
            ["Smtp:Username"] = "user",
            ["Smtp:Password"] = "password",
            ["Smtp:FromAddress"] = "noreply@wattwise.test",
            ["Smtp:FromName"] = "Watt-Wise",
        });

        IEmailSender sender = provider.GetRequiredService<IEmailSender>();

        Assert.IsType<SmtpEmailSender>(sender);
    }
}
