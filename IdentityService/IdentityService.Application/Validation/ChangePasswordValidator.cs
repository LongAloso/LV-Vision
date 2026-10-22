using FluentValidation;
using IdentityService.Application.DTOs.Auth;

namespace IdentityService.Application.Validation;

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequestDto>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("CurrentPassword is required.");

        RuleFor(x => x.NewPassword)
            .ApplyStandardPasswordRules("NewPassword");
    }
}
