namespace WattWise.Jobs.IntegrationTests;

public class JobsAssemblyTests
{
    [Fact]
    public void LoadsWithExpectedName()
    {
        Assert.Equal("WattWise.Jobs", typeof(Program).Assembly.GetName().Name);
    }
}
