using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WattWise.Application.Accounts;
using WattWise.Domain.Localization;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.Tests.Email;

public class SmtpEmailSenderFailureTests
{
    private const string Recipient = "victim@example.test";
    private const string FailurePrefix = "Sending EmailConfirmationEmail email in Lt failed with ";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static EmailConfirmationEmail Email => new(Recipient, Language.Lt, new Uri("https://app.wattwise.lt/confirm?t=1"));

    private static SmtpEmailSender SenderFor(
        int port, ListLogger<SmtpEmailSender> logger, SmtpSecurity security = SmtpSecurity.None, Action<SmtpSettings>? configure = null)
    {
        SmtpSettings settings = CompleteSmtpSettings.Create();
        settings.Host = "127.0.0.1";
        settings.Port = port;
        settings.Security = security;
        configure?.Invoke(settings);
        return new SmtpEmailSender(Options.Create(settings), new EmailRenderer(), logger);
    }

    private static int ClosedPort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<(Exception? Exception, ListLogger<SmtpEmailSender> Logger)> CancelWhileTheServerIsStalled()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Stalling();
        ListLogger<SmtpEmailSender> logger = new();
        SmtpEmailSender sender = SenderFor(server.Port, logger);
        using CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);

        Task sending = sender.SendAsync(Email, cancel.Token);
        await server.FirstCommandReceived.WaitAsync(TimeSpan.FromSeconds(10), Token);
        await cancel.CancelAsync();

        return (await Record.ExceptionAsync(() => sending), logger);
    }

    [Fact]
    public async Task A_refused_connection_is_logged_at_error_with_the_kind_language_and_exception_type_and_does_not_throw()
    {
        ListLogger<SmtpEmailSender> logger = new();

        await SenderFor(ClosedPort(), logger).SendAsync(Email, Token);

        Assert.Equal([(LogLevel.Error, FailurePrefix + "SocketException")], logger.LevelsAndMessages);
    }

    [Fact]
    public async Task A_rejection_by_the_server_is_logged_with_its_status_code_and_does_not_throw()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Rejecting("550 5.7.1 Mailbox unavailable");
        ListLogger<SmtpEmailSender> logger = new();

        await SenderFor(server.Port, logger).SendAsync(Email, Token);

        Assert.Equal(
            [(LogLevel.Error, FailurePrefix + "SmtpCommandException and SMTP status MailboxUnavailable")],
            logger.LevelsAndMessages);
    }

    [Fact]
    public async Task A_server_reply_that_echoes_the_recipient_never_reaches_the_log()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Rejecting($"550 5.1.1 <{Recipient}> user unknown");
        ListLogger<SmtpEmailSender> logger = new();

        await SenderFor(server.Port, logger).SendAsync(Email, Token);

        Assert.DoesNotContain(Recipient, logger.Everything, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejected_credentials_are_logged_as_an_authentication_failure_and_do_not_throw()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.RejectingCredentials();
        ListLogger<SmtpEmailSender> logger = new();

        await SenderFor(server.Port, logger).SendAsync(Email, Token);

        Assert.Equal([(LogLevel.Error, FailurePrefix + "AuthenticationException")], logger.LevelsAndMessages);
    }

    [Fact]
    public async Task Rejected_credentials_never_reach_the_log()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.RejectingCredentials();
        ListLogger<SmtpEmailSender> logger = new();
        // Generated, so the pre-commit secret scan has no literal to flag.
        string username = $"login-{Guid.NewGuid():N}";
        string password = $"pass-{Guid.NewGuid():N}";
        SmtpEmailSender sender = SenderFor(server.Port, logger, configure: settings =>
        {
            settings.Username = username;
            settings.Password = password;
        });

        await sender.SendAsync(Email, Token);

        Assert.DoesNotMatch($"{username}|{password}", logger.Everything);
    }

    [Fact]
    public async Task A_connection_dropped_after_ehlo_is_logged_and_does_not_throw()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.DroppingAfterEhlo();
        ListLogger<SmtpEmailSender> logger = new();

        await SenderFor(server.Port, logger).SendAsync(Email, Token);

        Assert.Equal([(LogLevel.Error, FailurePrefix + "SmtpProtocolException")], logger.LevelsAndMessages);
    }

    [Fact]
    public async Task Starttls_against_a_server_without_it_is_logged_and_does_not_throw()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.WithoutAuth();
        ListLogger<SmtpEmailSender> logger = new();

        await SenderFor(server.Port, logger, SmtpSecurity.StartTls).SendAsync(Email, Token);

        Assert.Equal([(LogLevel.Error, FailurePrefix + "NotSupportedException")], logger.LevelsAndMessages);
    }

    [Fact]
    public async Task Starttls_against_a_server_without_it_sends_no_credentials()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.WithoutAuth();

        await SenderFor(server.Port, new ListLogger<SmtpEmailSender>(), SmtpSecurity.StartTls).SendAsync(Email, Token);

        Assert.DoesNotContain(server.Commands, command => command.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Tls_from_the_first_byte_against_a_plain_text_server_is_logged_and_does_not_throw()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Accepting();
        ListLogger<SmtpEmailSender> logger = new();

        await SenderFor(server.Port, logger, SmtpSecurity.SslOnConnect).SendAsync(Email, Token);

        Assert.Equal([(LogLevel.Error, FailurePrefix + "SslHandshakeException")], logger.LevelsAndMessages);
    }

    [Fact]
    public async Task A_server_that_offers_no_authentication_is_logged_and_does_not_throw()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.WithoutAuth();
        ListLogger<SmtpEmailSender> logger = new();

        await SenderFor(server.Port, logger).SendAsync(Email, Token);

        Assert.Equal([(LogLevel.Error, FailurePrefix + "NotSupportedException")], logger.LevelsAndMessages);
    }

    [Fact]
    public async Task The_ehlo_greeting_uses_the_from_domain()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Accepting();

        await SenderFor(server.Port, new ListLogger<SmtpEmailSender>()).SendAsync(Email, Token);

        Assert.Equal("EHLO wattwise.test", server.Commands[0]);
    }

    [Fact]
    public async Task The_sender_authenticates_before_it_submits_the_message()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Accepting();

        await SenderFor(server.Port, new ListLogger<SmtpEmailSender>()).SendAsync(Email, Token);

        Assert.StartsWith("AUTH PLAIN", server.Commands.First(command => !command.StartsWith("EHLO", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_sender_ends_the_session_with_quit()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Accepting();

        await SenderFor(server.Port, new ListLogger<SmtpEmailSender>()).SendAsync(Email, Token);

        Assert.Equal("QUIT", server.Commands[^1]);
    }

    [Fact]
    public async Task A_successful_send_is_logged_at_debug_with_the_kind_and_language()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Accepting();
        ListLogger<SmtpEmailSender> logger = new();

        await SenderFor(server.Port, logger).SendAsync(Email, Token);

        Assert.Equal([(LogLevel.Debug, "Sent EmailConfirmationEmail email in Lt")], logger.LevelsAndMessages);
    }

    [Fact]
    public async Task Cancelling_during_the_send_propagates()
    {
        (Exception? exception, _) = await CancelWhileTheServerIsStalled();

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    [Fact]
    public async Task Cancelling_during_the_send_logs_nothing()
    {
        (_, ListLogger<SmtpEmailSender> logger) = await CancelWhileTheServerIsStalled();

        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData(null, "user", "password", "noreply@wattwise.test", "Smtp:Host is not set.")]
    [InlineData("127.0.0.1", " ", "password", "noreply@wattwise.test", "Smtp:Username is not set.")]
    [InlineData("127.0.0.1", "user", null, "noreply@wattwise.test", "Smtp:Password is not set.")]
    [InlineData("127.0.0.1", "user", "password", "", "Smtp:FromAddress is not set.")]
    public async Task A_setting_missing_at_send_time_throws_naming_it_instead_of_being_logged(
        string? host, string? username, string? password, string? fromAddress, string message)
    {
        SmtpSettings settings = CompleteSmtpSettings.Create();
        settings.Host = host;
        settings.Username = username;
        settings.Password = password;
        settings.FromAddress = fromAddress;
        SmtpEmailSender sender = new(Options.Create(settings), new EmailRenderer(), new ListLogger<SmtpEmailSender>());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(Email, Token));

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public async Task An_unset_security_mode_at_send_time_throws_instead_of_connecting_in_plain_text()
    {
        SmtpSettings settings = CompleteSmtpSettings.Create();
        settings.Security = null;
        SmtpEmailSender sender = new(Options.Create(settings), new EmailRenderer(), new ListLogger<SmtpEmailSender>());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(Email, Token));

        Assert.Equal("The Smtp:Security setting is not valid.", exception.Message);
    }

    // MailKit's spans carry the server address, the operation and an error type, but no recipient or server reply.
    [Fact]
    public async Task MailKits_spans_never_carry_the_recipient_or_the_servers_reply()
    {
        MailKit.Telemetry.SmtpClient.Configure();
        string recipient = $"{Guid.NewGuid():N}@example.test";
        ConcurrentSpanText spans = new();
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == MailKit.Telemetry.SmtpClient.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = spans.Add,
        };
        ActivitySource.AddActivityListener(listener);
        using ScriptedSmtpServer server = ScriptedSmtpServer.Rejecting($"550 5.1.1 <{recipient}> user unknown");

        await SenderFor(server.Port, new ListLogger<SmtpEmailSender>()).SendAsync(new EmailConfirmationEmail(recipient, Language.Lt, new Uri("https://x.test/")), Token);

        Assert.DoesNotContain(recipient, spans.Everything, StringComparison.Ordinal);
    }
}
