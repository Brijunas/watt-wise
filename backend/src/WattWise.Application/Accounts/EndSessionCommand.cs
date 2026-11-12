using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>Ends one session of the account. An id that is unknown or belongs to another account is not found.</summary>
/// <param name="AccountId">The account's id, from the access token's <c>sub</c> claim.</param>
/// <param name="SessionId">The session to end.</param>
public sealed record EndSessionCommand(Guid AccountId, Guid SessionId) : ICommand<Result<Unit>>;
