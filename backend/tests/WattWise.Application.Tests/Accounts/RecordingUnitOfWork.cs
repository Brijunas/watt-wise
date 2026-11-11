namespace WattWise.Application.Tests.Accounts;

internal sealed class RecordingUnitOfWork(List<string> calls) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        calls.Add("Save");
        return Task.CompletedTask;
    }
}
