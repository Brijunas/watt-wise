using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WattWise.Application.Accounts;
using WattWise.Application.Email;
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
        int port,
        ListLogger<SmtpEmailSender> logger,
        SmtpSecurity security = SmtpSecurity.None,
        Action<SmtpSettings>? configure = null,
        int timeoutMilliseconds = 120_000)
    {
        SmtpSettings settings = CompleteSmtpSettings.Create();
        settings.Host = "127.0.0.1";
        settings.Port = port;
        settings.Security = security;
        configure?.Invoke(settings);
        return new SmtpEmailSender(Options.Create(settings), new EmailRenderer(), logger, timeoutMilliseconds);
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

        Task sending = sender.SendAsync(Email, "test.1", cancel.Token);
        await server.FirstCommandReceived.WaitAsync(TimeSpan.FromSeconds(10), Token);
        await cancel.CancelAsync();

        return (await Record.ExceptionAsync(() => sending), logger);
    }

    public static TheoryData<Func<ScriptedSmtpServer>, string> RetryableReplies => new()
    {
        { () => ScriptedSmtpServer.Rejecting("451 4.3.0 Try later"), "SmtpCommandException 451" },
        { () => ScriptedSmtpServer.RejectingRecipient("450 4.2.1 Mailbox busy"), "SmtpCommandException 450" },
        { () => ScriptedSmtpServer.RejectingData("452 4.3.1 Insufficient storage"), "SmtpCommandException 452" },
        { () => ScriptedSmtpServer.RejectingMessage("451 4.3.0 Aborted"), "SmtpCommandException 451" },
        { () => ScriptedSmtpServer.Rejecting("421 4.4.2 Service closing"), "SmtpCommandException 421" },
    };

    public static TheoryData<Func<ScriptedSmtpServer>, string> PermanentReplies => new()
    {
        { () => ScriptedSmtpServer.Rejecting("550 5.7.1 Mailbox unavailable"), "SmtpCommandException 550" },
        { () => ScriptedSmtpServer.RejectingRecipient("550 5.1.1 User unknown"), "SmtpCommandException 550" },
        { () => ScriptedSmtpServer.RejectingData("554 5.3.0 Rejected"), "SmtpCommandException 554" },
        { () => ScriptedSmtpServer.RejectingMessage("552 5.2.2 Over quota"), "SmtpCommandException 552" },
        { () => ScriptedSmtpServer.Rejecting("500 5.5.1 Syntax error"), "SmtpCommandException 500" },
    };

    public static TheoryData<Func<ScriptedSmtpServer>, SmtpSecurity, string> TransportFailures => new()
    {
        { ScriptedSmtpServer.DroppingAfterEhlo, SmtpSecurity.None, "SmtpProtocolException" },
        { ScriptedSmtpServer.RejectingCredentials, SmtpSecurity.None, "AuthenticationException" },
        { ScriptedSmtpServer.WithoutAuth, SmtpSecurity.None, "NotSupportedException" },
        { ScriptedSmtpServer.WithoutAuth, SmtpSecurity.StartTls, "NotSupportedException" },
        { ScriptedSmtpServer.Accepting, SmtpSecurity.SslOnConnect, "SslHandshakeException" },
    };

    [Fact]
    public async Task An_accepted_message_is_sent_with_no_error()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Accepting();

        SendResult result = await SenderFor(server.Port, new ListLogger<SmtpEmailSender>()).SendAsync(Email, "42.1", Token);

        Assert.Equal(new SendResult(SendOutcome.Sent, null), result);
    }

    [Theory]
    [MemberData(nameof(RetryableReplies))]
    public async Task A_4xx_reply_in_any_phase_is_retryable_with_its_code(Func<ScriptedSmtpServer> start, string error)
    {
        using ScriptedSmtpServer server = start();

        SendResult result = await SenderFor(server.Port, new ListLogger<SmtpEmailSender>()).SendAsync(Email, "42.1", Token);

        Assert.Equal(new SendResult(SendOutcome.Retryable, error), result);
    }

    [Theory]
    [MemberData(nameof(PermanentReplies))]
    public async Task A_5xx_reply_in_any_phase_is_permanent_with_its_code(Func<ScriptedSmtpServer> start, string error)
    {
        using ScriptedSmtpServer server = start();

        SendResult result = await SenderFor(server.Port, new ListLogger<SmtpEmailSender>()).SendAsync(Email, "42.1", Token);

        Assert.Equal(new SendResult(SendOutcome.Permanent, error), result);
    }

    [Fact]
    public async Task A_reply_that_echoes_the_recipient_leaves_only_the_code_in_the_error()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.RejectingRecipient($"550 5.1.1 <{Recipient}> user unknown");

        SendResult result = await SenderFor(server.Port, new ListLogger<SmtpEmailSender>()).SendAsync(Email, "42.1", Token);

        Assert.Equal(new SendResult(SendOutcome.Permanent, "SmtpCommandException 550"), result);
    }

    [Fact]
    public async Task A_refused_connection_is_retryable()
    {
        SendResult result = await SenderFor(ClosedPort(), new ListLogger<SmtpEmailSender>()).SendAsync(Email, "42.1", Token);

        Assert.Equal(new SendResult(SendOutcome.Retryable, "SocketException"), result);
    }

    [Theory]
    [MemberData(nameof(TransportFailures))]
    public async Task A_protocol_authentication_or_tls_failure_is_retryable_with_the_exception_type(
        Func<ScriptedSmtpServer> start, SmtpSecurity security, string error)
    {
        using ScriptedSmtpServer server = start();

        SendResult result = await SenderFor(server.Port, new ListLogger<SmtpEmailSender>(), security).SendAsync(Email, "42.1", Token);

        Assert.Equal(new SendResult(SendOutcome.Retryable, error), result);
    }

    [Fact]
    public async Task A_stalled_server_is_retryable_once_the_timeout_passes()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Stalling();

        SendResult result = await SenderFor(server.Port, new ListLogger<SmtpEmailSender>(), timeoutMilliseconds: 1_000).SendAsync(Email, "42.1", Token);

        Assert.Equal(new SendResult(SendOutcome.Retryable, "TimeoutException"), result);
    }

    [Theory]
    [InlineData("42.1", "<42.1@wattwise.test>")]
    [InlineData("42.2", "<42.2@wattwise.test>")]
    public async Task The_message_id_is_the_local_part_at_the_from_domain(string localPart, string messageId)
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Accepting();

        await SenderFor(server.Port, new ListLogger<SmtpEmailSender>()).SendAsync(Email, localPart, Token);

        Assert.Equal([messageId], server.MessageIds);
    }

    [Fact]
    public async Task A_recipient_that_is_not_an_address_throws_instead_of_being_retried()
    {
        SmtpEmailSender sender = SenderFor(ClosedPort(), new ListLogger<SmtpEmailSender>());

        await Assert.ThrowsAsync<MimeKit.ParseException>(() => sender.SendAsync(new EmailConfirmationEmail("@@", Language.Lt, new Uri("https://x.test/")), "42.1", Token));
    }

    [Fact]
    public async Task A_refused_connection_is_logged_at_error_with_the_kind_language_and_exception_type()
    {
        ListLogger<SmtpEmailSender> logger = new();

        await SenderFor(ClosedPort(), logger).SendAsync(Email, "test.1", Token);

        Assert.Equal([(LogLevel.Error, FailurePrefix + "SocketException")], logger.LevelsAndMessages);
    }

    [Fact]
    public async Task A_rejection_by_the_server_is_logged_with_its_status_code()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Rejecting("550 5.7.1 Mailbox unavailable");
        ListLogger<SmtpEmailSender> logger = new();

        await SenderFor(server.Port, logger).SendAsync(Email, "test.1", Token);

        Assert.Equal(
            [(LogLevel.Error, FailurePrefix + "SmtpCommandException and SMTP status MailboxUnavailable")],
            logger.LevelsAndMessages);
    }

    [Fact]
    public async Task A_server_reply_that_echoes_the_recipient_never_reaches_the_log()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Rejecting($"550 5.1.1 <{Recipient}> user unknown");
        ListLogger<SmtpEmailSender> logger = new();

        await SenderFor(server.Port, logger).SendAsync(Email, "test.1", Token);

        Assert.DoesNotContain(Recipient, logger.Everything, StringComparison.Ordinal);
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

        await sender.SendAsync(Email, "test.1", Token);

        Assert.DoesNotMatch($"{username}|{password}", logger.Everything);
    }

    [Fact]
    public async Task Starttls_against_a_server_without_it_sends_no_credentials()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.WithoutAuth();

        await SenderFor(server.Port, new ListLogger<SmtpEmailSender>(), SmtpSecurity.StartTls).SendAsync(Email, "test.1", Token);

        Assert.DoesNotContain(server.Commands, command => command.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_ehlo_greeting_uses_the_from_domain()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Accepting();

        await SenderFor(server.Port, new ListLogger<SmtpEmailSender>()).SendAsync(Email, "test.1", Token);

        Assert.Equal("EHLO wattwise.test", server.Commands[0]);
    }

    [Fact]
    public async Task The_sender_authenticates_before_it_submits_the_message()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Accepting();

        await SenderFor(server.Port, new ListLogger<SmtpEmailSender>()).SendAsync(Email, "test.1", Token);

        Assert.StartsWith("AUTH PLAIN", server.Commands.First(command => !command.StartsWith("EHLO", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_sender_ends_the_session_with_quit()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Accepting();

        await SenderFor(server.Port, new ListLogger<SmtpEmailSender>()).SendAsync(Email, "test.1", Token);

        Assert.Equal("QUIT", server.Commands[^1]);
    }

    [Fact]
    public async Task A_successful_send_is_logged_at_debug_with_the_kind_and_language()
    {
        using ScriptedSmtpServer server = ScriptedSmtpServer.Accepting();
        ListLogger<SmtpEmailSender> logger = new();

        await SenderFor(server.Port, logger).SendAsync(Email, "test.1", Token);

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

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(Email, "test.1", Token));

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public async Task An_unset_security_mode_at_send_time_throws_instead_of_connecting_in_plain_text()
    {
        SmtpSettings settings = CompleteSmtpSettings.Create();
        settings.Security = null;
        SmtpEmailSender sender = new(Options.Create(settings), new EmailRenderer(), new ListLogger<SmtpEmailSender>());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(Email, "test.1", Token));

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

        await SenderFor(server.Port, new ListLogger<SmtpEmailSender>()).SendAsync(new EmailConfirmationEmail(recipient, Language.Lt, new Uri("https://x.test/")), "test.1", Token);

        Assert.DoesNotContain(recipient, spans.Everything, StringComparison.Ordinal);
    }
}
