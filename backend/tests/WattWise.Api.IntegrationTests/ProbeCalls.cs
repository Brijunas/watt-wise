namespace WattWise.Api.IntegrationTests;

/// <summary>Counts the requests that reached a <see cref="TestProbeEndpointModule"/> endpoint; one per host.</summary>
public sealed class ProbeCalls
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Increment() => Interlocked.Increment(ref _count);
}
