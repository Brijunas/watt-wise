namespace WattWise.Api.Endpoints;

/// <param name="Email">The address to send a new confirmation link to.</param>
public sealed record ResendConfirmationRequest(string? Email)
{
    public override string ToString() => nameof(ResendConfirmationRequest);
}
