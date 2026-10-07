namespace WattWise.Jobs.Recurring;

/// <summary>
/// Placeholder recurring job and the template for thin job classes: a real job only sends an
/// Application use case through the mediator and holds no logic of its own.
/// </summary>
public sealed class NoOpJob(ILogger<NoOpJob> logger)
{
    public const string RecurringJobId = "no-op";

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Hangfire cancels the token when the server shuts down; a real job passes it on to mediator.Send.
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("No-op job ran");
        return Task.CompletedTask;
    }
}
