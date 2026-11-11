using System.Collections.Concurrent;

using WattWise.Application.Email;

namespace WattWise.Jobs.IntegrationTests;

/// <summary>Stands in for SMTP: remembers each message with its Message-Id local part and reports it as sent.</summary>
internal sealed class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<(EmailMessage Message, string MessageIdLocalPart)> Sent { get; } = new();

    public Task<SendResult> SendAsync(EmailMessage message, string messageIdLocalPart, CancellationToken cancellationToken)
    {
        Sent.Enqueue((message, messageIdLocalPart));
        return Task.FromResult(SendResult.Sent);
    }
}
