namespace WattWise.Infrastructure.Email;

/// <summary>An email ready to send: a single-line subject, the plain-text part and the HTML part.</summary>
internal sealed record RenderedEmail(string Subject, string Text, string Html);
