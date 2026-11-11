namespace WattWise.Application.Email;

/// <summary>The retry schedule of one email kind: how many attempts it gets and the delay before each.</summary>
/// <param name="Attempts">The number of attempts, at least 1.</param>
/// <param name="Delays">One delay per attempt, the wait before it; <c>Delays[0]</c> is the wait before the first attempt.</param>
public sealed record EmailRetrySchedule(int Attempts, IReadOnlyList<TimeSpan> Delays);
