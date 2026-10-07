namespace WattWise.Jobs.IntegrationTests;

/// <summary>
/// Hangfire keeps process-wide static state (<c>GlobalConfiguration</c>, <c>JobStorage.Current</c>), so two
/// Jobs hosts running at once would share one storage. Every test class that starts a Jobs host joins this
/// collection, and its tests run one host at a time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class JobsHostCollection
{
    public const string Name = "Jobs host";
}
