using WattWise.Domain.Accounts;

namespace WattWise.Application.Accounts;

internal static class SessionEnding
{
    private const int MaxAttempts = 3;

    /// <summary>
    /// Deletes the session the token belongs to. When a save loses a race with another request that changed the session's
    /// tokens, the session is loaded again and the delete retried, so a refresh or sign-out that runs at the same time can't
    /// leave the session alive.
    /// </summary>
    public static async Task EndAsync(
        ISessionRepository sessions, IUnitOfWork unitOfWork, byte[] tokenHash, CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            Session? session = await sessions.GetByRefreshTokenHashAsync(tokenHash, cancellationToken);
            if (session is null)
            {
                return;
            }

            sessions.Remove(session);
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                // Load it again.
            }
        }
    }
}
