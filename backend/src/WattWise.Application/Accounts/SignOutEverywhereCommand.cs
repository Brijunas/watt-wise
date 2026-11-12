using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>Ends every session of the account, the caller's included.</summary>
/// <param name="AccountId">The account's id, from the access token's <c>sub</c> claim.</param>
public sealed record SignOutEverywhereCommand(Guid AccountId) : ICommand<Result<Unit>>;
