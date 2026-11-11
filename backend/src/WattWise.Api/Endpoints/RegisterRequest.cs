namespace WattWise.Api.Endpoints;

/// <param name="Email">The address to sign up with.</param>
/// <param name="Password">The password; the rules are in security.md.</param>
/// <param name="Language">The preferred language: <c>lt</c> or <c>en</c>.</param>
public sealed record RegisterRequest(string? Email, string? Password, string? Language)
{
    public override string ToString() => nameof(RegisterRequest);
}
