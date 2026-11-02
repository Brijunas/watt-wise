using Microsoft.AspNetCore.Http.HttpResults;

using WattWise.Api.ErrorHandling;
using WattWise.Application.Results;

namespace WattWise.Api.Tests.ErrorHandling;

public class ResultHttpExtensionsTests
{
    [Fact]
    public void A_success_maps_to_Ok_with_the_value()
    {
        Ok<string> ok = Assert.IsType<Ok<string>>(Result<string>.Success("x").ToHttpResult().Result);

        Assert.Equal("x", ok.Value);
    }

    [Fact]
    public void A_not_found_error_maps_to_a_problem_with_its_status_detail_and_code()
    {
        Result<string> failure = Error.NotFound("Plan.NotFound", "Plan 5 does not exist.");

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(failure.ToHttpResult().Result);

        Assert.Equal(
            (404, "Plan 5 does not exist.", "Plan.NotFound"),
            (problem.StatusCode, problem.ProblemDetails.Detail, problem.ProblemDetails.Extensions["code"]));
    }

    [Fact]
    public void A_validation_error_maps_to_a_400_validation_problem_with_the_validation_code()
    {
        Result<string> failure = Error.Validation(new Dictionary<string, string[]> { ["Name"] = ["bad"] });

        ValidationProblem problem = Assert.IsType<ValidationProblem>(failure.ToHttpResult().Result);

        Assert.Equal((400, "Validation.Failed"), (problem.StatusCode, problem.ProblemDetails.Extensions["code"]));
    }

    [Fact]
    public void Validation_field_keys_are_camel_cased_per_segment_and_keep_indexers_and_messages()
    {
        Result<string> failure = Error.Validation(new Dictionary<string, string[]>
        {
            ["Address.StreetName"] = ["a"],
            ["Items[0].Name"] = ["b"],
            ["IPAddress"] = ["c"],
        });

        ValidationProblem problem = Assert.IsType<ValidationProblem>(failure.ToHttpResult().Result);

        Assert.Equal(
            new Dictionary<string, string[]> { ["address.streetName"] = ["a"], ["items[0].name"] = ["b"], ["ipAddress"] = ["c"] },
            problem.ProblemDetails.Errors);
    }

    // characterization: ResultHttpExtensions.cs:28 (a Validation error without field errors becomes a plain problem)
    [Fact]
    public void A_validation_error_without_field_errors_maps_to_a_400_problem_not_a_validation_problem()
    {
        Result<string> failure = new Error("X.Y", "d", ErrorType.Validation);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(failure.ToHttpResult().Result);

        Assert.Equal((400, "X.Y"), (problem.StatusCode, problem.ProblemDetails.Extensions["code"]));
    }
}
