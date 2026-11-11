namespace WattWise.Application.Accounts;

/// <summary>What the signed-in account may see about itself.</summary>
/// <param name="Email">The sign-in email.</param>
/// <param name="Language">The preferred language: <c>lt</c> or <c>en</c>.</param>
/// <param name="Roles">Role names, sorted.</param>
/// <param name="Permissions">Permission names granted by the roles, sorted and without duplicates.</param>
public sealed record Me(string Email, string Language, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);
