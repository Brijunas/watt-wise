using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;
using NodaTime.Testing;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>Issues and redeems email links through <see cref="UserManager{TUser}"/>, each call in a scope of its own, the way requests do.</summary>
internal static class EmailLinks
{
    public static FakeClock NewClock() => new(Instant.FromUtc(2026, 10, 26, 12, 0));

    public static string Purpose(EmailLinkKind kind) => kind switch
    {
        EmailLinkKind.Confirmation => "EmailConfirmation",
        EmailLinkKind.Reset => "ResetPassword",
    };

    public static string[] Codes(IdentityResult result) => [.. result.Errors.Select(error => error.Code)];

    public static Task<string> IssueAsync(ServiceProvider provider, Guid identityId, EmailLinkKind kind) =>
        AccountsTestSupport.InScopeAsync(provider, identityId, (users, accountIdentity) => kind switch
        {
            EmailLinkKind.Confirmation => users.GenerateEmailConfirmationTokenAsync(accountIdentity),
            EmailLinkKind.Reset => users.GeneratePasswordResetTokenAsync(accountIdentity),
        });

    /// <summary>Uses the link: confirms the email, or sets <paramref name="newPassword"/> (a fresh valid one by default).</summary>
    public static Task<IdentityResult> RedeemAsync(
        ServiceProvider provider, Guid identityId, EmailLinkKind kind, string token, string? newPassword = null) =>
        AccountsTestSupport.InScopeAsync(provider, identityId, (users, accountIdentity) => kind switch
        {
            EmailLinkKind.Confirmation => users.ConfirmEmailAsync(accountIdentity, token),
            EmailLinkKind.Reset => users.ResetPasswordAsync(accountIdentity, token, newPassword ?? AccountsTestSupport.ValidPassword()),
        });

    public static async Task<byte[]> StoredHashAsync(ServiceProvider provider, Guid identityId, EmailLinkKind kind)
    {
        string purpose = Purpose(kind);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database
            .SqlQuery<byte[]>($"""
                SELECT token_hash AS "Value" FROM app.email_link_tokens
                WHERE identity_id = {identityId} AND purpose = {purpose}
                """)
            .SingleAsync(AccountsTestSupport.Token);
    }
}
