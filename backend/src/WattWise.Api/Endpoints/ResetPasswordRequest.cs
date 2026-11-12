namespace WattWise.Api.Endpoints;

/// <param name="Id">The <c>AccountIdentityId</c> from the link (a GUID).</param>
/// <param name="Token">The token from the link.</param>
/// <param name="NewPassword">The new password.</param>
public sealed record ResetPasswordRequest(string? Id, string? Token, string? NewPassword)
{
    // A record would print the token and the password.
    public override string ToString() => nameof(ResetPasswordRequest);
}
