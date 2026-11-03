using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MimeKit;
using MimeKit.Utils;

using WattWise.Application.Email;
using WattWise.Domain.Localization;

namespace WattWise.Infrastructure.Email;

/// <summary>
/// Renders an email and sends it through the configured SMTP server, on a new connection per message.
/// The Message-Id and the EHLO greeting use the from address's domain, so neither reveals the machine's hostname.
/// A failed send is logged and doesn't throw, because the user can ask for the email again.
/// </summary>
internal sealed partial class SmtpEmailSender(
    IOptions<SmtpSettings> options,
    EmailRenderer renderer,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    // A stalled server must not hold a request for MailKit's default of two minutes.
    private const int TimeoutMilliseconds = 30_000;

    private readonly SmtpSettings _settings = options.Value;

    public async Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
    {
        RenderedEmail rendered = await renderer.RenderAsync(email, cancellationToken);
        string domain = _settings.FromDomain;

        // Validated at start; the guards turn a settings bug into a clear error instead of a null.
        string host = Required(_settings.Host, "Host");
        string username = Required(_settings.Username, "Username");
        string password = Required(_settings.Password, "Password");
        string fromAddress = Required(_settings.FromAddress, "FromAddress");

        MimeMessage message = new()
        {
            MessageId = MimeUtils.GenerateMessageId(domain),
            Subject = rendered.Subject,
            Body = new BodyBuilder { TextBody = rendered.Text, HtmlBody = rendered.Html }.ToMessageBody(),
        };
        message.From.Add(new MailboxAddress(_settings.FromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(email.To));

        string emailKind = email.GetType().Name;

        try
        {
            using SmtpClient client = new() { LocalDomain = domain, Timeout = TimeoutMilliseconds };
            await client.ConnectAsync(host, _settings.Port, SocketOptions(_settings.Security), cancellationToken);
            await client.AuthenticateAsync(username, password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            LogSent(emailKind, email.Language);
        }
        catch (SmtpCommandException exception)
        {
            // The exception's message can echo the recipient (a 550 reply often does), so it is never logged.
            LogSendFailedWithStatus(emailKind, email.Language, exception.GetType().Name, exception.StatusCode);
        }
        catch (Exception exception) when (exception is
            SmtpProtocolException
            or MailKit.Security.AuthenticationException
            or SslHandshakeException
            or System.Net.Sockets.SocketException
            or IOException
            or MailKit.ServiceNotConnectedException
            or MailKit.ServiceNotAuthenticatedException
            or NotSupportedException
            or TimeoutException)
        {
            LogSendFailed(emailKind, email.Language, exception.GetType().Name);
        }
    }

    private static string Required(string? value, string key) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"Smtp:{key} is not set.") : value;

    private static SecureSocketOptions SocketOptions(SmtpSecurity? security) => security switch
    {
        SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        SmtpSecurity.None => SecureSocketOptions.None,
        _ => throw new InvalidOperationException("The Smtp:Security setting is not valid."),
    };

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sent {EmailKind} email in {Language}")]
    private partial void LogSent(string emailKind, Language language);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Sending {EmailKind} email in {Language} failed with {ErrorType} and SMTP status {SmtpStatusCode}")]
    private partial void LogSendFailedWithStatus(string emailKind, Language language, string errorType, SmtpStatusCode smtpStatusCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sending {EmailKind} email in {Language} failed with {ErrorType}")]
    private partial void LogSendFailed(string emailKind, Language language, string errorType);
}
