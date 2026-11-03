namespace WattWise.Testing;

/// <summary>The <c>Smtp:*</c> configuration keys for tests: a real server's address, or dummies for hosts that never send.</summary>
public static class SmtpTestSettings
{
    public const string FromAddress = "noreply@wattwise.test";

    public const string FromName = "Watt-Wise";

    /// <summary>Complete, valid settings that point nowhere; enough for a host to start in any environment.</summary>
    public static IReadOnlyDictionary<string, string?> Dummy => For("localhost", 587, "StartTls");

    /// <summary>Settings for an SMTP server, by default a plain-text one such as Mailpit, with any credentials.</summary>
    public static IReadOnlyDictionary<string, string?> For(string host, int port, string security = "None") =>
        new Dictionary<string, string?>
        {
            ["Smtp:Host"] = host,
            ["Smtp:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Smtp:Security"] = security,
            ["Smtp:Username"] = "test-user",
            ["Smtp:Password"] = "test-password",
            ["Smtp:FromAddress"] = FromAddress,
            ["Smtp:FromName"] = FromName,
        };
}
