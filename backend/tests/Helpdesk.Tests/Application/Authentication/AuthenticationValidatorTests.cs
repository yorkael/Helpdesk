using System.Linq.Expressions;
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

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Registration_name_cannot_exceed_the_column_size(int length, bool isValid)
    {
        var result = _registerValidator.TestValidate(new RegisterRequest(new string('a', length), "ana@example.com", ValidPassword));

        AssertValidity(result, request => request.Name, isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Registration_requires_a_valid_email(string email)
    {
        var result = _registerValidator.TestValidate(new RegisterRequest("Ana Ruiz", email, ValidPassword));

        result.ShouldHaveValidationErrorFor(request => request.Email);
    }

    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void Registration_email_cannot_exceed_the_column_size(int length, bool isValid)
    {
        var result = _registerValidator.TestValidate(new RegisterRequest("Ana Ruiz", EmailOfLength(length), ValidPassword));

        AssertValidity(result, request => request.Email, isValid);
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Registration_password_length_must_be_between_8_and_128(int length, bool isValid)
    {
        var result = _registerValidator.TestValidate(new RegisterRequest("Ana Ruiz", "ana@example.com", new string('p', length)));

        AssertValidity(result, request => request.Password, isValid);
    }

    // Long enough to pass the length rule, so only NotEmpty can reject it.
    [Fact]
    public void Registration_password_of_only_spaces_is_rejected()
    {
        var result = _registerValidator.TestValidate(new RegisterRequest("Ana Ruiz", "ana@example.com", new string(' ', 8)));

        result.ShouldHaveValidationErrorFor(request => request.Password);
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

    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void Login_email_cannot_exceed_the_column_size(int length, bool isValid)
    {
        var result = _loginValidator.TestValidate(new LoginRequest(EmailOfLength(length), ValidPassword));

        AssertValidity(result, request => request.Email, isValid);
    }

    [Theory]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Login_password_cannot_exceed_128_characters(int length, bool isValid)
    {
        var result = _loginValidator.TestValidate(new LoginRequest("ana@example.com", new string('p', length)));

        AssertValidity(result, request => request.Password, isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refresh_token_is_required(string refreshToken)
    {
        var result = _refreshTokenValidator.TestValidate(new RefreshTokenRequest(refreshToken));

        result.ShouldHaveValidationErrorFor(request => request.RefreshToken);
    }

    [Theory]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Refresh_token_cannot_exceed_128_characters(int length, bool isValid)
    {
        var result = _refreshTokenValidator.TestValidate(new RefreshTokenRequest(new string('t', length)));

        AssertValidity(result, request => request.RefreshToken, isValid);
    }

    private static string EmailOfLength(int length)
    {
        const string domain = "@example.com";
        return new string('a', length - domain.Length) + domain;
    }

    private static void AssertValidity<TRequest, TProperty>(
        TestValidationResult<TRequest> result,
        Expression<Func<TRequest, TProperty>> property,
        bool isValid)
    {
        if (isValid)
        {
            result.ShouldNotHaveValidationErrorFor(property);
        }
        else
        {
            result.ShouldHaveValidationErrorFor(property);
        }
    }
}
