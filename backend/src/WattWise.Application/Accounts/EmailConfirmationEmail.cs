using WattWise.Application.Email;
using WattWise.Domain.Localization;

namespace WattWise.Application.Accounts;

/// <summary>Asks a new account's owner to confirm the email address with a single-use link.</summary>
public sealed record EmailConfirmationEmail(string To, Language Language, Uri Link) : EmailMessage(To, Language);
