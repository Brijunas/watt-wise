namespace WattWise.Infrastructure.IntegrationTests.Email;

/// <summary>What the <see cref="FaultyTokenProvider"/> does for the AccountIdentity <see cref="IdentityId"/>: throws <see cref="Exception"/>, after tracking a token row when <see cref="TrackPartialToken"/>. Filled in once the account exists.</summary>
internal sealed class TokenFault
{
    public Guid IdentityId { get; set; }

    public Exception Exception { get; set; } = new InvalidOperationException("token failure");

    public bool TrackPartialToken { get; set; }
}
