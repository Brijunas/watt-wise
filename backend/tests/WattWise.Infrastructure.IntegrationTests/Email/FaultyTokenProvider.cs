using Microsoft.AspNetCore.Identity;

using NodaTime;

using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Email;

/// <summary>
/// The real <see cref="EmailLinkTokenProvider"/> for every AccountIdentity but one: for that one it throws the configured
/// exception, after adding a half-made token row to the context when asked, the way a failure partway through a token write would.
/// </summary>
internal sealed class FaultyTokenProvider(AppDbContext context, IClock clock, TokenFault fault) : IUserTwoFactorTokenProvider<AccountIdentity>
{
    private readonly EmailLinkTokenProvider _real = new(context, clock);

    public Task<string> GenerateAsync(string purpose, UserManager<AccountIdentity> manager, AccountIdentity user)
    {
        if (user.Id != fault.IdentityId)
        {
            return _real.GenerateAsync(purpose, manager, user);
        }

        if (fault.TrackPartialToken)
        {
            context.EmailLinkTokens.Add(new EmailLinkToken
            {
                AccountIdentityId = user.Id,
                Purpose = purpose,
                TokenHash = new byte[32],
                ExpiresAt = clock.GetCurrentInstant() + Duration.FromHours(1),
                CreatedAt = clock.GetCurrentInstant(),
            });
        }

        throw fault.Exception;
    }

    public Task<bool> ValidateAsync(string purpose, string token, UserManager<AccountIdentity> manager, AccountIdentity user) =>
        _real.ValidateAsync(purpose, token, manager, user);

    public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<AccountIdentity> manager, AccountIdentity user) =>
        _real.CanGenerateTwoFactorTokenAsync(manager, user);
}
