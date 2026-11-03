using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Configuration;

using MimeKit;

namespace WattWise.Infrastructure.Email;

/// <summary>SMTP connection and sender settings, read from the "Smtp" configuration section.</summary>
/// <remarks>A class rather than a record, so ToString never prints the password.</remarks>
public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string? Host { get; set; }

    public int Port { get; set; }

    public SmtpSecurity? Security { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? FromAddress { get; set; }

    public string? FromName { get; set; }

    /// <summary>The domain of <see cref="FromAddress"/>, used for the Message-Id and the EHLO greeting; empty when the address is invalid.</summary>
    public string FromDomain => TryParseFromAddress(out MailboxAddress? mailbox) ? DomainOf(mailbox) : string.Empty;

    /// <summary>Fills this instance from the section. Blank or non-numeric numbers stay 0; a blank or unknown security mode stays unset.</summary>
    public void ReadFrom(IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(SectionName);
        Host = section["Host"];
        Port = int.TryParse(section["Port"], out int port) ? port : 0;
        Security = Enum.TryParse(section["Security"], ignoreCase: true, out SmtpSecurity security) && Enum.IsDefined(security)
            ? security
            : null;
        Username = section["Username"];
        Password = section["Password"];
        FromAddress = section["FromAddress"];
        FromName = section["FromName"];
    }

    /// <summary>
    /// The keys that are missing or invalid, as "Smtp:Key"; empty when the settings are usable. Unencrypted
    /// SMTP (<see cref="SmtpSecurity.None"/>) is only valid in Development.
    /// </summary>
    public IReadOnlyList<string> MissingKeys(bool isDevelopment)
    {
        List<string> missing = [];
        if (string.IsNullOrWhiteSpace(Host)) { missing.Add("Host"); }
        if (Port is <= 0 or > 65535) { missing.Add("Port"); }
        if (Security is null || (Security == SmtpSecurity.None && !isDevelopment)) { missing.Add("Security"); }
        if (string.IsNullOrWhiteSpace(Username)) { missing.Add("Username"); }
        if (string.IsNullOrWhiteSpace(Password)) { missing.Add("Password"); }
        if (!TryParseFromAddress(out _)) { missing.Add("FromAddress"); }
        if (string.IsNullOrWhiteSpace(FromName)) { missing.Add("FromName"); }

        return [.. missing.Select(key => $"{SectionName}:{key}")];
    }

    /// <summary>True when <see cref="FromAddress"/> is a plain address with a domain, without a display name.</summary>
    private bool TryParseFromAddress([NotNullWhen(true)] out MailboxAddress? mailbox)
    {
        mailbox = null;
        if (string.IsNullOrWhiteSpace(FromAddress)
            || !MailboxAddress.TryParse(FromAddress, out MailboxAddress? parsed)
            || parsed.Address != FromAddress.Trim()
            || DomainOf(parsed).Length == 0)
        {
            return false;
        }

        mailbox = parsed;
        return true;
    }

    private static string DomainOf(MailboxAddress mailbox)
    {
        int at = mailbox.Address.LastIndexOf('@');
        return at < 0 ? string.Empty : mailbox.Address[(at + 1)..];
    }
}
