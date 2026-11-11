using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

internal sealed class RecordingEmailConfirmation(List<string> calls) : IEmailConfirmation
{
    /// <summary>What every call answers.</summary>
    public bool Valid { get; set; } = true;

    public Task<bool> ConfirmAsync(Guid accountIdentityId, string token, CancellationToken cancellationToken)
    {
        calls.Add($"Confirm:{accountIdentityId}:{token}");
        return Task.FromResult(Valid);
    }
}
