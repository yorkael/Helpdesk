using FluentValidation;

namespace Helpdesk.Application.Authentication;

internal static class AuthenticationLimits
{
    // Match the column sizes in UserConfiguration.
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 256;

    public const int PasswordMinLength = 8;

    // Caps the work spent hashing an attacker-supplied password.
    public const int PasswordMaxLength = 128;

    public const int RefreshTokenMaxLength = 128;
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(AuthenticationLimits.NameMaxLength);

        RuleFor(request => request.Email)
            .NotEmpty()
            .MaximumLength(AuthenticationLimits.EmailMaxLength)
            .EmailAddress();

        RuleFor(request => request.Password)
            .NotEmpty()
            .Length(AuthenticationLimits.PasswordMinLength, AuthenticationLimits.PasswordMaxLength);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .MaximumLength(AuthenticationLimits.EmailMaxLength);

        RuleFor(request => request.Password)
            .NotEmpty()
            .MaximumLength(AuthenticationLimits.PasswordMaxLength);
    }
}

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(request => request.RefreshToken)
            .NotEmpty()
            .MaximumLength(AuthenticationLimits.RefreshTokenMaxLength);
    }
}
