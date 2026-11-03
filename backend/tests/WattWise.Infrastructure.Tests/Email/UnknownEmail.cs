using WattWise.Application.Email;
using WattWise.Domain.Localization;

namespace WattWise.Infrastructure.Tests.Email;

/// <summary>A message kind the renderer has no template for.</summary>
internal sealed record UnknownEmail(string To, Language Language) : EmailMessage(To, Language);
