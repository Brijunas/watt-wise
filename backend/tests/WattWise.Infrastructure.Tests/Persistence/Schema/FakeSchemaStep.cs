using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Infrastructure.Tests.Persistence.Schema;

internal sealed class FakeSchemaStep(string name, List<string> log, Exception? failure = null) : ISchemaStep
{
    public string Name => name;

    public Task ApplyAsync(CancellationToken cancellationToken)
    {
        log.Add(name);
        return failure is null ? Task.CompletedTask : Task.FromException(failure);
    }
}
