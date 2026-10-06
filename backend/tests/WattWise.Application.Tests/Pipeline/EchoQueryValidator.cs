using FluentValidation;

namespace WattWise.Application.Tests.Pipeline;

public sealed class EchoQueryValidator : AbstractValidator<EchoQuery>
{
    public EchoQueryValidator()
    {
        RuleFor(query => query.Name).NotEmpty().MaximumLength(20);
    }
}
