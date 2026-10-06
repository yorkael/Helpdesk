namespace Helpdesk.Application.Authentication;

/// <summary>
/// Same message for an unknown email, a wrong password and an inactive account,
/// so the response does not reveal which accounts exist.
/// </summary>
public sealed class InvalidCredentialsException() : Exception("Invalid email or password.");

public sealed class InvalidRefreshTokenException() : Exception("The refresh token is invalid or expired.");

public sealed class EmailAlreadyRegisteredException(Exception? innerException = null)
    : Exception("The email is already registered.", innerException);
