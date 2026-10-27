using WattWise.Domain.Exceptions;

namespace WattWise.Domain.Tests.Exceptions;

internal sealed class TestDomainException : DomainException
{
    public TestDomainException(string code, string message)
        : base(code, message)
    {
    }

    public TestDomainException(string code, string message, Exception innerException)
        : base(code, message, innerException)
    {
    }
}
