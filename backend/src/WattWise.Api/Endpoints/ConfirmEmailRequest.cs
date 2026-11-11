namespace WattWise.Api.Endpoints;

/// <param name="Id">The <c>AccountIdentityId</c> from the link (a GUID).</param>
/// <param name="Token">The token from the link.</param>
public sealed record ConfirmEmailRequest(string? Id, string? Token)
{
    public override string ToString() => nameof(ConfirmEmailRequest);
}
