using WattWise.Application;

namespace WattWise.Application.Tests;

public class ApplicationAssemblyTests
{
    [Fact]
    public void LoadsWithExpectedName()
    {
        Assert.Equal("WattWise.Application", typeof(ApplicationAssembly).Assembly.GetName().Name);
    }
}
