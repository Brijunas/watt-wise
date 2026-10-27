namespace WattWise.Domain.Tests.Exceptions;

public class DomainExceptionTests
{
    [Fact]
    public void The_two_argument_constructor_keeps_the_code_and_the_message()
    {
        TestDomainException exception = new("Area.Reason", "A message.");

        Assert.Equal(("Area.Reason", "A message."), (exception.Code, exception.Message));
    }

    [Fact]
    public void The_three_argument_constructor_keeps_the_code_the_message_and_the_inner_exception()
    {
        InvalidOperationException inner = new("Inner.");
        TestDomainException exception = new("Area.Reason", "A message.", inner);

        Assert.Equal(("Area.Reason", "A message.", (Exception)inner), (exception.Code, exception.Message, exception.InnerException));
    }
}
