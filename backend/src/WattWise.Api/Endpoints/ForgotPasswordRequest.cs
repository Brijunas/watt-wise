namespace WattWise.Api.Endpoints;

/// <param name="Email">The address to send a password reset link to.</param>
public sealed record ForgotPasswordRequest(string? Email)
{
    public override string ToString() => nameof(ForgotPasswordRequest);
}
