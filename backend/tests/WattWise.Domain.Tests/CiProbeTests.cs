namespace WattWise.Domain.Tests;

// Throwaway (S5B.1): a failing test the PR check must catch. Never merged.
public class CiProbeTests
{
    [Fact]
    public void Fails_on_purpose()
    {
        Assert.Equal(3, 1 + 1);
    }
}
