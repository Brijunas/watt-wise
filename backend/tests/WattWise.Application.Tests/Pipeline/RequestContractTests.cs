namespace WattWise.Application.Tests.Pipeline;

public class RequestContractTests
{
    [Fact]
    public void Every_application_request_returns_a_result()
    {
        IReadOnlyList<Type> messages = RequestContract.MessagesIn(typeof(ApplicationAssembly).Assembly);
        Assert.NotEmpty(messages);

        Assert.DoesNotContain(messages, message => !RequestContract.ReturnsResult(message));
    }

    [Fact]
    public void The_helper_finds_queries_and_commands_and_tells_which_return_a_result()
    {
        Type[] expected = [typeof(CreateThingCommand), typeof(EchoQuery), typeof(UnwrappedQuery)];

        IEnumerable<(Type, bool)> found = RequestContract.MessagesIn(typeof(RequestContractTests).Assembly)
            .Where(expected.Contains)
            .OrderBy(message => message.Name, StringComparer.Ordinal)
            .Select(message => (message, RequestContract.ReturnsResult(message)));

        Assert.Equal([(typeof(CreateThingCommand), true), (typeof(EchoQuery), true), (typeof(UnwrappedQuery), false)], found);
    }
}
