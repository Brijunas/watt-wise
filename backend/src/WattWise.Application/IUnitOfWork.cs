namespace WattWise.Application;

/// <summary>Saves everything a use case staged (an account change and its email request) in one transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
