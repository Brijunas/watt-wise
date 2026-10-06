using FluentValidation;

namespace WattWise.Application.Tests.Pipeline;

public sealed class EchoQueryValidator : AbstractValidator<EchoQuery>
{
    public EchoQueryValidator()
    {
        RuleFor(query => query.Name).NotEmpty()
            .MaximumLength(20)
            .Matches("^[^0-9]*$").WithMessage("'Name' must not contain digits.");
    }
}
