using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Infrastructure.Tests.Persistence;

/// <summary>A hand-written <see cref="ISchemaStep"/> that records that it ran and then does what the test says.</summary>
internal sealed class FakeSchemaStep(string name, List<string> executed, Func<Task>? behavior = null) : ISchemaStep
{
    public string Name => name;

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        executed.Add(name);
        if (behavior is not null)
        {
            await behavior();
        }
    }
}
