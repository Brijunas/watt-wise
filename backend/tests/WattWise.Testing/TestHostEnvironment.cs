using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace WattWise.Testing;

/// <summary>A host environment with a given name, for service providers built without a host.</summary>
public sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "WattWise.Tests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
