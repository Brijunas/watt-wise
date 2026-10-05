using WattWise.Infrastructure;

namespace WattWise.Infrastructure.IntegrationTests;

public class InfrastructureAssemblyTests
{
    [Fact]
    public void LoadsWithExpectedName()
    {
        Assert.Equal("WattWise.Infrastructure", typeof(InfrastructureAssembly).Assembly.GetName().Name);
    }
}
