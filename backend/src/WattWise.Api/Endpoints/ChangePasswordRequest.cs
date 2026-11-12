namespace WattWise.Api.Endpoints;

/// <param name="CurrentPassword">The account's password now.</param>
/// <param name="NewPassword">The new password.</param>
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword)
{
    // A record would print the passwords.
    public override string ToString() => nameof(ChangePasswordRequest);
}
