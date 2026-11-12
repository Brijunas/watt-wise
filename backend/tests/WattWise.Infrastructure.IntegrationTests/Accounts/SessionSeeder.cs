using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using WattWise.Application;
using WattWise.Application.Accounts;
using WattWise.Domain.Accounts;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>Stores sessions the way sign-in does, with the times a test chooses.</summary>
internal static class SessionSeeder
{
    /// <summary>A session started at <paramref name="signedInAt"/> whose first refresh token expires at <paramref name="expiresAt"/>.</summary>
    public static Session Start(Guid accountId, string label, Instant signedInAt, Instant expiresAt) =>
        Session.Start(accountId, label, "stamp", Guid.NewGuid().ToByteArray(), signedInAt, expiresAt);

    /// <summary>A session whose first token (expiring at <paramref name="firstExpiresAt"/>) was exchanged at <paramref name="rotatedAt"/> for one expiring at <paramref name="secondExpiresAt"/>.</summary>
    public static Session StartRotated(
        Guid accountId, string label, Instant signedInAt, Instant firstExpiresAt, Instant rotatedAt, Instant secondExpiresAt)
    {
        byte[] first = Guid.NewGuid().ToByteArray();
        Session session = Session.Start(accountId, label, "stamp", first, signedInAt, firstExpiresAt);
        session.Rotate(first, Guid.NewGuid().ToByteArray(), rotatedAt, secondExpiresAt);
        return session;
    }

    public static async Task AddAsync(ServiceProvider provider, Session session)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ISessionRepository>().Add(session);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(AccountsTestSupport.Token);
    }
}
