namespace WattWise.Infrastructure.Email;

/// <summary>How the connection to the SMTP server is secured.</summary>
public enum SmtpSecurity
{
    /// <summary>Connects in plain text and upgrades with STARTTLS (port 587).</summary>
    StartTls,

    /// <summary>TLS from the first byte (port 465).</summary>
    SslOnConnect,

    /// <summary>No encryption. Only for Development's Mailpit.</summary>
    None,
}
