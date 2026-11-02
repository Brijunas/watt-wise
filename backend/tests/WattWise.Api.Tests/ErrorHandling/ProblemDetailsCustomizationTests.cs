using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using WattWise.Api.ErrorHandling;

namespace WattWise.Api.Tests.ErrorHandling;

public class ProblemDetailsCustomizationTests
{
    [Fact]
    public void A_problem_without_a_code_gets_General_plus_the_reason_phrase()
    {
        // The response status stays 200, so only Status can give NotFound.
        Assert.Equal("General.NotFound", CodeAfterApply(status: 404, responseStatus: 200));
    }

    [Fact]
    public void A_hyphen_and_spaces_are_removed_from_the_reason_phrase()
    {
        // RFC 9110 15.3.4: "Non-Authoritative Information".
        Assert.Equal("General.NonAuthoritativeInformation", CodeAfterApply(status: 203, responseStatus: 200));
    }

    [Fact]
    public void A_500_gets_General_Unexpected()
    {
        Assert.Equal("General.Unexpected", CodeAfterApply(status: 500, responseStatus: 200));
    }

    // characterization: ProblemDetailsCustomization.cs:34 (no reason phrase for 599)
    [Fact]
    public void A_status_without_a_reason_phrase_gets_Status_plus_the_number()
    {
        Assert.Equal("General.Status599", CodeAfterApply(status: 599, responseStatus: 200));
    }

    [Fact]
    public void An_existing_code_is_kept()
    {
        Assert.Equal("Plan.NotFound", CodeAfterApply(status: 404, responseStatus: 200, existingCode: "Plan.NotFound"));
    }

    [Fact]
    public void A_problem_without_a_status_uses_the_response_status_code()
    {
        Assert.Equal("General.NotFound", CodeAfterApply(status: null, responseStatus: 404));
    }

    private static object? CodeAfterApply(int? status, int responseStatus, string? existingCode = null)
    {
        DefaultHttpContext http = new();
        http.Response.StatusCode = responseStatus;
        ProblemDetails details = new() { Status = status };
        if (existingCode is not null)
        {
            details.Extensions["code"] = existingCode;
        }

        ProblemDetailsCustomization.Apply(new ProblemDetailsContext { HttpContext = http, ProblemDetails = details });

        return details.Extensions["code"];
    }
}
