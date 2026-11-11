namespace WattWise.Application.Email;

/// <summary>Sends a transactional email. Rendering and delivery are Infrastructure's.</summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends the message. Delivery failures come back as a <see cref="SendResult"/>; cancellation and bugs
    /// (a broken template, a missing setting, a recipient that isn't an address) still throw.
    /// </summary>
    /// <param name="message">What to send and in which language.</param>
    /// <param name="messageIdLocalPart">The Message-Id's local part; the sender appends <c>@&lt;from domain&gt;</c>.</param>
    Task<SendResult> SendAsync(EmailMessage message, string messageIdLocalPart, CancellationToken cancellationToken);
}
