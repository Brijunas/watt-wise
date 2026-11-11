using FluentValidation;

namespace WattWise.Application.Accounts;

internal static class EmailRules
{
    /// <summary>Required, a valid address and at most 254 characters, ignoring surrounding spaces.</summary>
    public static IRuleBuilderOptions<T, string?> MustBeEmail<T>(this IRuleBuilderInitial<T, string?> rule)
    {
        return rule
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(email => email!.Trim().Length <= 254)
            .WithMessage("'{PropertyName}' must be at most 254 characters.")
            .Must(email => new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email!.Trim()))
            .WithMessage("'{PropertyName}' is not a valid email address.");
    }
}
