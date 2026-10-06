using FluentValidation.TestHelper;
using Helpdesk.Application.Authentication;

namespace Helpdesk.Tests.Application.Authentication;

public class AuthenticationValidatorTests
{
    private const string ValidPassword = "correct horse battery";

    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly LoginRequestValidator _loginValidator = new();
    private readonly RefreshTokenRequestValidator _refreshTokenValidator = new();

    [Fact]
    public void Valid_registration_passes()
    {
        var result = _registerValidator.TestValidate(new RegisterRequest("Ana Ruiz", "ana@example.com", ValidPassword));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Registration_requires_a_name(string name)
    {
        var result = _registerValidator.TestValidate(new RegisterRequest(name, "ana@example.com", ValidPassword));

        result.ShouldHaveValidationErrorFor(request => request.Name);
    }

    [Fact]
    public void Registration_name_longer_than_the_column_is_rejected()
    {
        var result = _registerValidator.TestValidate(new RegisterRequest(new string('a', 101), "ana@example.com", ValidPassword));

        result.ShouldHaveValidationErrorFor(request => request.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Registration_requires_a_valid_email(string email)
    {
        var result = _registerValidator.TestValidate(new RegisterRequest("Ana Ruiz", email, ValidPassword));

        result.ShouldHaveValidationErrorFor(request => request.Email);
    }

    [Fact]
    public void Registration_email_longer_than_the_column_is_rejected()
    {
        var email = new string('a', 245) + "@example.com";

        var result = _registerValidator.TestValidate(new RegisterRequest("Ana Ruiz", email, ValidPassword));

        result.ShouldHaveValidationErrorFor(request => request.Email);
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Registration_password_length_must_be_between_8_and_128(int length, bool isValid)
    {
        var result = _registerValidator.TestValidate(new RegisterRequest("Ana Ruiz", "ana@example.com", new string('p', length)));

        if (isValid)
        {
            result.ShouldNotHaveValidationErrorFor(request => request.Password);
        }
        else
        {
            result.ShouldHaveValidationErrorFor(request => request.Password);
        }
    }

    [Fact]
    public void Login_requires_email_and_password()
    {
        var result = _loginValidator.TestValidate(new LoginRequest("", ""));

        result.ShouldHaveValidationErrorFor(request => request.Email);
        result.ShouldHaveValidationErrorFor(request => request.Password);
    }

    [Fact]
    public void Login_does_not_enforce_the_minimum_password_length()
    {
        var result = _loginValidator.TestValidate(new LoginRequest("ana@example.com", "short"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Login_password_longer_than_128_is_rejected()
    {
        var result = _loginValidator.TestValidate(new LoginRequest("ana@example.com", new string('p', 129)));

        result.ShouldHaveValidationErrorFor(request => request.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refresh_token_is_required(string refreshToken)
    {
        var result = _refreshTokenValidator.TestValidate(new RefreshTokenRequest(refreshToken));

        result.ShouldHaveValidationErrorFor(request => request.RefreshToken);
    }

    [Fact]
    public void Refresh_token_longer_than_128_is_rejected()
    {
        var result = _refreshTokenValidator.TestValidate(new RefreshTokenRequest(new string('t', 129)));

        result.ShouldHaveValidationErrorFor(request => request.RefreshToken);
    }
}
