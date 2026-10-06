namespace WattWise.Api.IntegrationTests;

/// <summary>Walks an exception and its inner exceptions, outermost first.</summary>
public static class ExceptionChain
{
    public static IEnumerable<Exception> Of(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }
}
