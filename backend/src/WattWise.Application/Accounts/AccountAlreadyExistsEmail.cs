using WattWise.Application.Email;
using WattWise.Domain.Localization;

namespace WattWise.Application.Accounts;

/// <summary>Tells the owner of an existing account that someone tried to sign up with the same email address.</summary>
public sealed record AccountAlreadyExistsEmail(string To, Language Language) : EmailMessage(To, Language);
