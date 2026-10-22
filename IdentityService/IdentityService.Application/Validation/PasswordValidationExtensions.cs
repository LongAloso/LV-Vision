using FluentValidation;

namespace IdentityService.Application.Validation;

internal static class PasswordValidationExtensions
{
    // P3.2: special char requirement aligns with checklist 2.1.3 R141.
    // Set is conservative — common ASCII symbols that work across keyboard layouts.
    private const string SpecialChars = "!@#$%^&*()_+-=[]{}|;:'\",.<>/?`~\\";

    public static IRuleBuilderOptions<T, string?> ApplyStandardPasswordRules<T>(this IRuleBuilder<T, string?> ruleBuilder, string fieldName = "Password")
    {
        return ruleBuilder
            .NotEmpty().WithMessage($"{fieldName} is required.")
            .MinimumLength(8).WithMessage($"{fieldName} must be at least 8 characters.")
            .MaximumLength(100).WithMessage($"{fieldName} must be at most 100 characters.")
            .Must(p => p is not null && !p.Contains(" ")).WithMessage($"{fieldName} must not contain spaces.")
            .Matches("[A-Z]").WithMessage($"{fieldName} must contain at least one uppercase letter (A-Z).")
            .Matches("[a-z]").WithMessage($"{fieldName} must contain at least one lowercase letter (a-z).")
            .Matches("[0-9]").WithMessage($"{fieldName} must contain at least one digit (0-9).")
            .Must(p => p is not null && p.IndexOfAny(SpecialChars.ToCharArray()) >= 0)
            .WithMessage($"{fieldName} must contain at least one special character (e.g., !@#$%^&*).");
    }
}
