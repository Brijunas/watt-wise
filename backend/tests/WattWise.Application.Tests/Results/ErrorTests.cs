using WattWise.Application.Results;
using WattWise.Application.Tests.Pipeline;

namespace WattWise.Application.Tests.Results;

public class ErrorTests
{
    [Fact]
    public void Validation_sets_the_code_the_type_and_the_field_errors()
    {
        Dictionary<string, string[]> fieldErrors = new() { ["name"] = ["Required."] };

        Error error = Error.Validation(fieldErrors);

        Assert.Equal(("Validation.Failed", ErrorType.Validation, fieldErrors), (error.Code, error.Type, error.FieldErrors));
    }

    public static TheoryData<Func<string, string, Error>, ErrorType> CategoryFactories => new()
    {
        { Error.NotFound, ErrorType.NotFound },
        { Error.Unauthorized, ErrorType.Unauthorized },
    };

    [Theory]
    [MemberData(nameof(CategoryFactories))]
    public void A_category_factory_sets_its_type_and_keeps_the_code_and_description(Func<string, string, Error> factory, ErrorType type)
    {
        Error error = factory("Test.Code", "Description.");

        Assert.Equal(new Error("Test.Code", "Description.", type), error);
    }

    [Theory]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Unauthorized)]
    public void From_copies_the_code_and_message_of_a_domain_exception_and_uses_the_given_type(ErrorType type)
    {
        TestDomainException exception = new("Test.Broken", "The invariant is broken.");

        Error error = Error.From(exception, type);

        Assert.Equal(new Error("Test.Broken", "The invariant is broken.", type), error);
    }
}
