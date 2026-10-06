using System.Text.Json;

using FluentValidation;
using FluentValidation.Results;

using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Behaviors;

/// <summary>
/// Runs every FluentValidation validator for the request. An invalid request gets a validation
/// error and never reaches the handler.
/// </summary>
public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
    where TResponse : IFallibleResult<TResponse>
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        IValidator<TMessage>[] validatorList = [.. validators];
        if (validatorList.Length == 0)
        {
            return await next(message, cancellationToken);
        }

        ValidationContext<TMessage> context = new(message);
        ValidationResult[] results = await Task.WhenAll(
            validatorList.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        Dictionary<string, string[]> fieldErrors = results
            .SelectMany(result => result.Errors)
            .GroupBy(failure => ToCamelCasePath(failure.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray());

        if (fieldErrors.Count == 0)
        {
            return await next(message, cancellationToken);
        }

        return TResponse.Failure(Error.Validation(fieldErrors));
    }

    private static string ToCamelCasePath(string propertyName)
    {
        return string.Join('.', propertyName.Split('.').Select(segment => JsonNamingPolicy.CamelCase.ConvertName(segment)));
    }
}
