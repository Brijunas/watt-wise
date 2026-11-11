using System.Collections.Concurrent;

using Microsoft.AspNetCore.Identity;

using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>The stock hasher with a log of what it was asked: <c>Hash</c>, or <c>Verify:&lt;provided password&gt;</c>.</summary>
internal sealed class RecordingPasswordHasher : IPasswordHasher<AccountIdentity>
{
    private readonly PasswordHasher<AccountIdentity> _inner = new();

    public ConcurrentQueue<string> Calls { get; } = new();

    public string HashPassword(AccountIdentity user, string password)
    {
        Calls.Enqueue("Hash");
        return _inner.HashPassword(user, password);
    }

    public PasswordVerificationResult VerifyHashedPassword(AccountIdentity user, string hashedPassword, string providedPassword)
    {
        Calls.Enqueue($"Verify:{providedPassword}");
        return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}
