using WattWise.Domain.Exceptions;

namespace WattWise.Api.IntegrationTests;

/// <summary>A domain exception no handler expects, so it must surface as a 500.</summary>
public sealed class TestDomainException() : DomainException("Test.Broken", "domain-secret-detail-456");
