namespace WattWise.Application.Tests.Pipeline;

/// <summary>Counts handler invocations. Registered per test host, so parallel tests don't share it.</summary>
public sealed class CallRecorder
{
    private int _calls;

    public int Calls => _calls;

    public void Record()
    {
        Interlocked.Increment(ref _calls);
    }
}
