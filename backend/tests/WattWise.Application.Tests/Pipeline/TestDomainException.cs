using WattWise.Domain.Exceptions;

namespace WattWise.Application.Tests.Pipeline;

public sealed class TestDomainException(string code, string message) : DomainException(code, message);
