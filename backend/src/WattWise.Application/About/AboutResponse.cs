namespace WattWise.Application.About;

/// <summary>What the API calls itself and the version it runs.</summary>
/// <param name="Name">The product name.</param>
/// <param name="Version">The informational version of the Application assembly.</param>
public sealed record AboutResponse(string Name, string Version);
