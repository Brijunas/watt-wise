using WattWise.Application.Results;

namespace WattWise.Application.Tests.Results;

public class ResultTests
{
    private static readonly Error SomeError = Error.NotFound("Test.Missing", "Missing.");

    [Fact]
    public void Implicit_conversion_from_a_value_gives_a_success()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Implicit_conversion_from_an_error_gives_a_failure()
    {
        Result<int> result = SomeError;

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Same(SomeError, result.Error);
    }

    [Fact]
    public void Value_throws_on_a_failure()
    {
        Result<int> result = SomeError;

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Error_throws_on_a_success()
    {
        Result<int> result = 42;

        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Match_runs_the_success_branch_for_a_success()
    {
        Result<int> result = 42;

        string text = result.Match(value => $"value {value}", error => $"error {error.Code}");

        Assert.Equal("value 42", text);
    }

    [Fact]
    public void Match_runs_the_failure_branch_for_a_failure()
    {
        Result<int> result = SomeError;

        string text = result.Match(value => $"value {value}", error => $"error {error.Code}");

        Assert.Equal("error Test.Missing", text);
    }
}
