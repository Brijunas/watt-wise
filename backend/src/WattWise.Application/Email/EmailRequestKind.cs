namespace WattWise.Application.Email;

/// <summary>The kind of email a request asks for. The names are stored as text in the outbox, so renaming one needs a migration.</summary>
public enum EmailRequestKind
{
    EmailConfirmation,
    AccountAlreadyExists,
    PasswordReset,
}
