namespace WattWise.Application.Email;

/// <summary>Sends a transactional email. Rendering and delivery are Infrastructure's.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage email, CancellationToken cancellationToken);
}
