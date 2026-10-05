namespace WattWise.Domain.Tests;

public class DomainAssemblyTests
{
    [Fact]
    public void LoadsWithExpectedName()
    {
        Assert.Equal("WattWise.Domain", typeof(DomainAssembly).Assembly.GetName().Name);
    }
}
