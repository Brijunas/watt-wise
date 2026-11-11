using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using WattWise.Application.Accounts;
using WattWise.Application.Email;
using WattWise.Domain.Localization;

namespace WattWise.Infrastructure.IntegrationTests.Email;

public class SmtpEmailSenderTests(MailpitContainerFixture mailpit)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Uri NewLink() => new($"https://app.wattwise.lt/confirm?token={Guid.NewGuid():N}");

    private static string NewRecipient() => $"{Guid.NewGuid():N}@example.test";

    public static TheoryData<Func<string, EmailMessage>, string> KindsAndSubjects => new()
    {
        { to => new EmailConfirmationEmail(to, Language.En, NewLink()), "Confirm your email for Watt-Wise" },
        { to => new PasswordResetEmail(to, Language.Lt, NewLink()), "Atkurkite „Watt-Wise“ slaptažodį" },
    };

    private ServiceProvider BuildServices(ListLoggerProvider? logs = null)
    {
        Dictionary<string, string?> settings = new(mailpit.SmtpConfiguration)
        {
            // AddInfrastructure registers the database settings; nothing here connects to a database.
            ["Database:Host"] = "localhost",
            ["Database:Port"] = "5432",
            ["Database:Name"] = "unused",
            ["Database:Username"] = "unused",
            ["Database:Password"] = "unused",
        };
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("Development"));
        services.AddLogging();
        if (logs is not null)
        {
            services.AddSingleton<ILoggerProvider>(logs);
        }

        services.AddInfrastructure();
        return services.BuildServiceProvider();
    }

    private async Task<MailpitMessage> SendAsync(EmailMessage email, ListLoggerProvider? logs = null, string localPart = "test.1")
    {
        await using ServiceProvider provider = BuildServices(logs);
        await provider.GetRequiredService<IEmailSender>().SendAsync(email, localPart, Token);
        return await mailpit.Client.WaitForMessageToAsync(email.To, Token);
    }

    [Theory]
    [MemberData(nameof(KindsAndSubjects))]
    public async Task A_kind_in_a_language_arrives_with_its_subject(Func<string, EmailMessage> create, string subject)
    {
        MailpitMessage message = await SendAsync(create(NewRecipient()));

        Assert.Equal(subject, message.Subject);
    }

    [Fact]
    public async Task The_message_arrives_for_its_recipient_only()
    {
        string recipient = NewRecipient();

        MailpitMessage message = await SendAsync(new AccountAlreadyExistsEmail(recipient, Language.En));

        Assert.Equal([recipient], message.To.Select(address => address.Address));
    }

    [Fact]
    public async Task The_link_arrives_in_the_text_part()
    {
        Uri link = NewLink();

        MailpitMessage message = await SendAsync(new EmailConfirmationEmail(NewRecipient(), Language.En, link));

        Assert.Contains(link.AbsoluteUri, message.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_link_arrives_in_the_html_part()
    {
        Uri link = NewLink();

        MailpitMessage message = await SendAsync(new EmailConfirmationEmail(NewRecipient(), Language.En, link));

        Assert.Contains(link.AbsoluteUri, message.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_message_comes_from_the_configured_name_and_address()
    {
        MailpitMessage message = await SendAsync(new AccountAlreadyExistsEmail(NewRecipient(), Language.En));

        Assert.Equal(new MailpitAddress("Watt-Wise", "noreply@wattwise.test"), message.From);
    }

    [Fact]
    public async Task The_message_id_is_the_local_part_at_the_from_domain()
    {
        MailpitMessage message = await SendAsync(new AccountAlreadyExistsEmail(NewRecipient(), Language.En), localPart: "7.1");

        Assert.Equal("7.1@wattwise.test", message.MessageId);
    }

    [Fact]
    public async Task A_delivered_message_is_reported_as_sent()
    {
        await using ServiceProvider provider = BuildServices();

        SendResult result = await provider.GetRequiredService<IEmailSender>()
            .SendAsync(new AccountAlreadyExistsEmail(NewRecipient(), Language.En), "7.1", Token);

        Assert.Equal(new SendResult(SendOutcome.Sent, null), result);
    }

    [Fact]
    public async Task A_successful_send_logs_no_error()
    {
        ListLoggerProvider logs = new();

        await SendAsync(new AccountAlreadyExistsEmail(NewRecipient(), Language.En), logs);

        Assert.DoesNotContain(logs.Entries, entry => entry.Level >= LogLevel.Error);
    }
}
