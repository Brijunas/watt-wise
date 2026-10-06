namespace WattWise.Application.Tests.Pipeline;

public class RequestContractTests
{
    [Fact]
    public void Every_application_request_returns_a_result()
    {
        IReadOnlyList<Type> messages = RequestContract.MessagesIn(typeof(ApplicationAssembly).Assembly);

        Assert.All(messages, message => Assert.True(
            RequestContract.ReturnsResult(message),
            $"{message.FullName} must return Result<T>."));
    }

    [Fact]
    public void Every_test_request_returns_a_result_and_the_check_finds_them()
    {
        IReadOnlyList<Type> messages = RequestContract.MessagesIn(typeof(RequestContractTests).Assembly);

        Assert.Contains(typeof(EchoQuery), messages);
        Assert.Contains(typeof(CreateThingCommand), messages);
        Assert.All(messages, message => Assert.True(
            RequestContract.ReturnsResult(message),
            $"{message.FullName} must return Result<T>."));
    }
}
