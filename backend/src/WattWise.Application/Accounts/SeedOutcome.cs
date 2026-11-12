namespace WattWise.Application.Accounts;

/// <summary>What seeding one smoke account did.</summary>
public enum SeedOutcome
{
    /// <summary>The account did not exist and was created.</summary>
    Created,

    /// <summary>The account existed and something about it differed and was changed.</summary>
    Changed,

    /// <summary>The account already was in the asked state; nothing was written.</summary>
    Unchanged,
}
