using NodaTime;

namespace WattWise.Application.Email;

/// <summary>What the worker does with a row after a send attempt.</summary>
public enum RetryAction
{
    /// <summary>Mark the row sent.</summary>
    Sent,

    /// <summary>Send the row back to pending, due at <see cref="RetryDecision.NextAttemptAt"/>.</summary>
    Retry,

    /// <summary>Mark the row failed.</summary>
    Failed,
}

/// <summary>The policy's verdict. <see cref="NextAttemptAt"/> is set for <see cref="RetryAction.Retry"/>, <see cref="Error"/> for Retry and Failed.</summary>
public sealed record RetryDecision(RetryAction Action, Instant? NextAttemptAt, string? Error);
