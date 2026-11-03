using WattWise.Domain.Localization;

namespace WattWise.Application.Email;

/// <summary>A transactional email: who gets it and in which language. Each kind derives from this and adds its own values.</summary>
public abstract record EmailMessage(string To, Language Language);
