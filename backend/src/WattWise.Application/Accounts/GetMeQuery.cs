using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>The signed-in account's own details.</summary>
/// <param name="AccountId">The account's id, from the access token's <c>sub</c> claim.</param>
public sealed record GetMeQuery(Guid AccountId) : IQuery<Result<Me>>;
