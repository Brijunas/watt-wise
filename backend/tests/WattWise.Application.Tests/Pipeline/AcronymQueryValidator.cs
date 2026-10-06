using FluentValidation;

namespace WattWise.Application.Tests.Pipeline;

public sealed class AcronymQueryValidator : AbstractValidator<AcronymQuery>
{
    public AcronymQueryValidator()
    {
        RuleFor(query => query.URL).NotEmpty();
        RuleFor(query => query.IPAddress).NotEmpty();
    }
}
