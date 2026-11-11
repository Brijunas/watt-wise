using WattWise.Application.Accounts;
using WattWise.Domain.Accounts;

namespace WattWise.Application.Tests.Accounts;

/// <summary>
/// The session port over a list: finds a session by the hash of any of its tokens and records adds and removes. A remove
/// takes effect at once, unless the unit of work is a <see cref="ConflictingUnitOfWork"/>, which undoes it when a save fails.
/// </summary>
internal sealed class InMemorySessions(List<string> calls) : ISessionRepository
{
    private readonly List<Session> _staged = [];

    public List<Session> Stored { get; } = [];

    public Task<Session?> GetByRefreshTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        Session? found = Stored.SingleOrDefault(session => session.RefreshTokens.Any(token => token.TokenHash.SequenceEqual(tokenHash)));
        return Task.FromResult(found);
    }

    public void Add(Session session)
    {
        calls.Add("AddSession");
        Stored.Add(session);
    }

    public void Remove(Session session)
    {
        calls.Add("RemoveSession");
        Stored.Remove(session);
        _staged.Add(session);
    }

    public void UndoStagedRemovals()
    {
        Stored.AddRange(_staged);
        _staged.Clear();
    }

    public void CommitStagedRemovals() => _staged.Clear();
}
