using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.Tests.Email;

internal static class CompleteSmtpSettings
{
    public static SmtpSettings Create() => new()
    {
        Host = "smtp.example.test",
        Port = 587,
        Security = SmtpSecurity.StartTls,
        Username = "user",
        // Generated, so the pre-commit secret scan has no literal to flag.
        Password = Guid.NewGuid().ToString("N"),
        FromAddress = "noreply@wattwise.test",
        FromName = "Watt-Wise",
    };
}
