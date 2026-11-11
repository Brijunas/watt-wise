using WattWise.Api.ErrorHandling;
using WattWise.Application.Results;

namespace WattWise.Api.Tests.ErrorHandling;

public class ErrorTypeMappingTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.Locked, 423)]
    [InlineData(ErrorType.BadRequest, 400)]
    [InlineData(ErrorType.Conflict, 409)]
    public void Each_error_type_maps_to_its_status(ErrorType type, int expected)
    {
        Assert.Equal(expected, ErrorTypeMapping.ToStatusCode(type));
    }
}
