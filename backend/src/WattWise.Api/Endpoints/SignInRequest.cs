namespace WattWise.Api.Endpoints;

/// <param name="Email">The address the account signed up with.</param>
/// <param name="Password">The account's password.</param>
public sealed record SignInRequest(string? Email, string? Password)
{
    // A record would print the password.
    public override string ToString() => nameof(SignInRequest);
}
