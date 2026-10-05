namespace WattWise.Api.IntegrationTests;

public class ApiAssemblyTests
{
    [Fact]
    public void LoadsWithExpectedName()
    {
        Assert.Equal("WattWise.Api", typeof(Program).Assembly.GetName().Name);
    }
}
