using System.Reflection;

using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Tests.Pipeline;

/// <summary>Finds the messages of an assembly and checks that each one returns a <see cref="Result{T}"/>.</summary>
public static class RequestContract
{
    private static readonly Type[] ResponseContracts = [typeof(IQuery<>), typeof(ICommand<>), typeof(IRequest<>)];

    /// <summary>
    /// Concrete queries, commands and requests. Notifications are excluded: they have no response
    /// and don't pass through the pipeline behaviors.
    /// </summary>
    public static IReadOnlyList<Type> MessagesIn(Assembly assembly)
    {
        return
        [
            .. assembly.GetTypes().Where(type =>
                type is { IsAbstract: false, IsInterface: false }
                && typeof(IMessage).IsAssignableFrom(type)
                && !typeof(INotification).IsAssignableFrom(type)),
        ];
    }

    /// <summary>Whether the message is an <see cref="IQuery{TResponse}"/>, <see cref="ICommand{TResponse}"/> or <see cref="IRequest{TResponse}"/> of a <see cref="Result{T}"/>.</summary>
    public static bool ReturnsResult(Type message)
    {
        return message.GetInterfaces().Any(contract =>
            contract.IsGenericType
            && ResponseContracts.Contains(contract.GetGenericTypeDefinition())
            && contract.GetGenericArguments()[0] is { IsGenericType: true } response
            && response.GetGenericTypeDefinition() == typeof(Result<>));
    }
}
