using FluentValidation;
using IdentityService.Application.DTOs.Auth;
using IdentityService.Domain.Interfaces;

namespace IdentityService.Application.Validation
{
    public sealed class LoginRequestValidator : AbstractValidator<LoginRequestDto>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.UserNameOrEmail)
                .NotEmpty()
                .WithMessage("UserNameOrEmail is required.");

            RuleFor(x => x.Password)
                .NotEmpty()
                .WithMessage("Password is required.");
        }
    }

    public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequestDto>
    {
        public RefreshTokenRequestValidator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty()
                .WithMessage("RefreshToken is required.");
        }
    }
    public sealed class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequestDto>
    {
        private readonly IUserRepository _userRepository;
        public RegisterUserRequestValidator(IUserRepository userRepository)
        {
            _userRepository = userRepository;
            RuleFor(x => x.UserName)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .WithMessage("Invalid email address.");

            RuleFor(x => x.Password)
                .ApplyStandardPasswordRules()
                .Must((req, p) => !string.Equals(p, req.UserName, StringComparison.OrdinalIgnoreCase))
                .WithMessage("Password must not match the username.");

        }
    }
}
