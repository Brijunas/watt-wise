using WattWise.Application.Email;
using WattWise.Domain.Localization;

namespace WattWise.Application.Accounts;

/// <summary>Gives an account's owner a single-use link to set a new password.</summary>
public sealed record PasswordResetEmail(string To, Language Language, Uri Link) : EmailMessage(To, Language);
