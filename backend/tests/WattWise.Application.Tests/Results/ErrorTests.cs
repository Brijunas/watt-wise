using WattWise.Application.Results;
using WattWise.Application.Tests.Pipeline;

namespace WattWise.Application.Tests.Results;

public class ErrorTests
{
    [Fact]
    public void From_copies_the_code_and_message_of_a_domain_exception()
    {
        TestDomainException exception = new("Test.Broken", "The invariant is broken.");

        Error error = Error.From(exception, ErrorType.NotFound);

        Assert.Equal("Test.Broken", error.Code);
        Assert.Equal("The invariant is broken.", error.Description);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Null(error.FieldErrors);
    }

    [Fact]
    public void Validation_sets_the_code_type_and_field_errors()
    {
        Dictionary<string, string[]> fieldErrors = new() { ["name"] = ["Required."] };

        Error error = Error.Validation(fieldErrors);

        Assert.Equal("Validation.Failed", error.Code);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Same(fieldErrors, error.FieldErrors);
    }

    [Fact]
    public void NotFound_sets_the_type()
    {
        Error error = Error.NotFound("Test.Missing", "Missing.");

        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal("Test.Missing", error.Code);
        Assert.Equal("Missing.", error.Description);
    }

    [Fact]
    public void Unauthorized_sets_the_type()
    {
        Error error = Error.Unauthorized("Test.Denied", "Denied.");

        Assert.Equal(ErrorType.Unauthorized, error.Type);
        Assert.Equal("Test.Denied", error.Code);
    }
}
