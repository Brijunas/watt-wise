using System.Diagnostics.CodeAnalysis;

namespace WattWise.Application.Accounts;

/// <summary>The result of changing a password. <see cref="SecurityStamp"/> is set exactly when <see cref="Kind"/> is <see cref="PasswordChangeKind.Changed"/>.</summary>
public sealed record PasswordChangeOutcome
{
    private PasswordChangeOutcome(PasswordChangeKind kind, string? securityStamp = null)
    {
        Kind = kind;
        SecurityStamp = securityStamp;
    }

    public PasswordChangeKind Kind { get; }

    public string? SecurityStamp { get; }

    [MemberNotNullWhen(true, nameof(SecurityStamp))]
    public bool IsChanged => Kind == PasswordChangeKind.Changed;

    public static PasswordChangeOutcome Changed(string securityStamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(securityStamp);
        return new PasswordChangeOutcome(PasswordChangeKind.Changed, securityStamp);
    }

    public static PasswordChangeOutcome WrongPassword() => new(PasswordChangeKind.WrongPassword);

    public static PasswordChangeOutcome NotFound() => new(PasswordChangeKind.NotFound);

    public static PasswordChangeOutcome Conflict() => new(PasswordChangeKind.Conflict);

    // A record would print the stamp.
    public override string ToString() => $"{nameof(PasswordChangeOutcome)} {Kind}";
}
