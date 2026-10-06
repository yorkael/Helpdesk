using FluentValidation;
using Helpdesk.Application.Authentication;
using Helpdesk.Application.Common;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;

namespace Helpdesk.Tests.Application.Authentication;

public class AuthServiceTests
{
    private const string Password = "correct horse battery";

    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryRefreshTokenRepository _refreshTokens = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeRefreshTokenIssuer _refreshTokenIssuer = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _authService = new AuthService(
            _users,
            _refreshTokens,
            _unitOfWork,
            _passwordHasher,
            new FakeAccessTokenIssuer(),
            _refreshTokenIssuer,
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            new RefreshTokenRequestValidator(),
            _time);
    }

    [Fact]
    public async Task Register_creates_a_client_with_a_hashed_password()
    {
        var response = await _authService.RegisterAsync(
            new RegisterRequest("Ana Ruiz", " Ana@Example.com ", Password), CancellationToken.None);

        var user = Assert.Single(_users.Users);
        Assert.Equal(UserRole.Client, user.Role);
        Assert.Equal("ana@example.com", user.Email);
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.Equal(_passwordHasher.Hash(Password), user.PasswordHash);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Equal(new UserResponse(user.Id, "Ana Ruiz", "ana@example.com", "Client"), response);
    }

    [Fact]
    public async Task Register_rejects_an_email_that_is_already_registered()
    {
        SeedUser("ana@example.com");

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() => _authService.RegisterAsync(
            new RegisterRequest("Ana Again", "ANA@example.com", Password), CancellationToken.None));

        Assert.Single(_users.Users);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Register_rejects_invalid_input_before_touching_storage()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _authService.RegisterAsync(
            new RegisterRequest("Ana Ruiz", "not-an-email", "short"), CancellationToken.None));

        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task Login_returns_tokens_and_stores_only_the_refresh_token_hash()
    {
        var user = SeedUser("ana@example.com");

        var response = await _authService.LoginAsync(new LoginRequest("Ana@Example.com", Password), CancellationToken.None);

        Assert.Equal($"access:{user.Id}:Client", response.AccessToken);
        Assert.Equal(_time.Now.AddMinutes(15), response.AccessTokenExpiresAt);
        var stored = Assert.Single(_refreshTokens.Tokens);
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal(_refreshTokenIssuer.Hash(response.RefreshToken), stored.TokenHash);
        Assert.NotEqual(response.RefreshToken, stored.TokenHash);
        Assert.Equal(stored.ExpiresAt, response.RefreshTokenExpiresAt);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Login_with_unknown_email_and_wrong_password_fail_the_same_way()
    {
        SeedUser("ana@example.com");

        var unknownEmail = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _authService.LoginAsync(new LoginRequest("nobody@example.com", Password), CancellationToken.None));
        var wrongPassword = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _authService.LoginAsync(new LoginRequest("ana@example.com", "wrong password"), CancellationToken.None));

        Assert.Equal(unknownEmail.Message, wrongPassword.Message);
        Assert.Empty(_refreshTokens.Tokens);
    }

    [Fact]
    public async Task Login_with_unknown_email_still_spends_a_password_verification()
    {
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _authService.LoginAsync(new LoginRequest("nobody@example.com", Password), CancellationToken.None));

        Assert.Equal(1, _passwordHasher.SimulatedVerifications);
    }

    [Fact]
    public async Task Login_rehashes_the_password_when_the_hasher_asks_for_it()
    {
        var user = SeedUser("ana@example.com");
        var oldHash = user.PasswordHash;
        _passwordHasher.CurrentVersion = 2;

        await _authService.LoginAsync(new LoginRequest("ana@example.com", Password), CancellationToken.None);

        Assert.NotEqual(oldHash, user.PasswordHash);
        Assert.Equal(_passwordHasher.Hash(Password), user.PasswordHash);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Login_of_an_inactive_user_fails_like_a_wrong_password()
    {
        var user = SeedUser("ana@example.com");
        Deactivate(user);

        var exception = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _authService.LoginAsync(new LoginRequest("ana@example.com", Password), CancellationToken.None));

        Assert.Equal(new InvalidCredentialsException().Message, exception.Message);
        Assert.Empty(_refreshTokens.Tokens);
    }

    [Fact]
    public async Task Login_rejects_invalid_input()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _authService.LoginAsync(new LoginRequest("", ""), CancellationToken.None));
    }

    [Fact]
    public async Task Refresh_rotates_the_token()
    {
        var user = SeedUser("ana@example.com");
        var login = await _authService.LoginAsync(new LoginRequest("ana@example.com", Password), CancellationToken.None);
        _time.Now = _time.Now.AddMinutes(20);

        var refreshed = await _authService.RefreshAsync(new RefreshTokenRequest(login.RefreshToken), CancellationToken.None);

        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        var oldToken = _refreshTokens.Tokens.Single(t => t.TokenHash == _refreshTokenIssuer.Hash(login.RefreshToken));
        var newToken = _refreshTokens.Tokens.Single(t => t.TokenHash == _refreshTokenIssuer.Hash(refreshed.RefreshToken));
        Assert.Equal(_time.Now, oldToken.RevokedAt);
        Assert.Equal(newToken.Id, oldToken.ReplacedByTokenId);
        Assert.True(newToken.IsActive(_time.Now));
        Assert.Equal(user.Id, newToken.UserId);
        Assert.Equal(_time.Now.AddMinutes(15), refreshed.AccessTokenExpiresAt);
    }

    [Fact]
    public async Task Refresh_rejects_an_unknown_token()
    {
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() =>
            _authService.RefreshAsync(new RefreshTokenRequest("refresh-unknown"), CancellationToken.None));
    }

    [Fact]
    public async Task Refresh_rejects_an_expired_token()
    {
        SeedUser("ana@example.com");
        var login = await _authService.LoginAsync(new LoginRequest("ana@example.com", Password), CancellationToken.None);
        _time.Now = login.RefreshTokenExpiresAt;

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() =>
            _authService.RefreshAsync(new RefreshTokenRequest(login.RefreshToken), CancellationToken.None));
    }

    [Fact]
    public async Task Refresh_of_an_inactive_user_is_rejected_without_issuing_a_new_token()
    {
        var user = SeedUser("ana@example.com");
        var login = await _authService.LoginAsync(new LoginRequest("ana@example.com", Password), CancellationToken.None);
        Deactivate(user);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() =>
            _authService.RefreshAsync(new RefreshTokenRequest(login.RefreshToken), CancellationToken.None));

        Assert.Single(_refreshTokens.Tokens);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_revokes_every_session_of_the_user()
    {
        SeedUser("ana@example.com");
        var firstSession = await _authService.LoginAsync(new LoginRequest("ana@example.com", Password), CancellationToken.None);
        var secondSession = await _authService.LoginAsync(new LoginRequest("ana@example.com", Password), CancellationToken.None);
        var rotated = await _authService.RefreshAsync(new RefreshTokenRequest(firstSession.RefreshToken), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() =>
            _authService.RefreshAsync(new RefreshTokenRequest(firstSession.RefreshToken), CancellationToken.None));

        Assert.All(_refreshTokens.Tokens, token => Assert.NotNull(token.RevokedAt));
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() =>
            _authService.RefreshAsync(new RefreshTokenRequest(rotated.RefreshToken), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() =>
            _authService.RefreshAsync(new RefreshTokenRequest(secondSession.RefreshToken), CancellationToken.None));
    }

    [Fact]
    public async Task Refresh_losing_a_concurrent_rotation_is_rejected_without_revoking_other_sessions()
    {
        SeedUser("ana@example.com");
        var firstSession = await _authService.LoginAsync(new LoginRequest("ana@example.com", Password), CancellationToken.None);
        var secondSession = await _authService.LoginAsync(new LoginRequest("ana@example.com", Password), CancellationToken.None);
        _unitOfWork.ExceptionOnNextSave = new ConcurrencyConflictException(new Exception());

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() =>
            _authService.RefreshAsync(new RefreshTokenRequest(firstSession.RefreshToken), CancellationToken.None));

        var otherSession = _refreshTokens.Tokens.Single(t => t.TokenHash == _refreshTokenIssuer.Hash(secondSession.RefreshToken));
        Assert.Null(otherSession.RevokedAt);
    }

    [Fact]
    public async Task Logout_revokes_the_token()
    {
        SeedUser("ana@example.com");
        var login = await _authService.LoginAsync(new LoginRequest("ana@example.com", Password), CancellationToken.None);

        await _authService.LogoutAsync(new RefreshTokenRequest(login.RefreshToken), CancellationToken.None);

        Assert.NotNull(Assert.Single(_refreshTokens.Tokens).RevokedAt);
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() =>
            _authService.RefreshAsync(new RefreshTokenRequest(login.RefreshToken), CancellationToken.None));
    }

    [Fact]
    public async Task Logout_ignores_unknown_and_already_revoked_tokens()
    {
        SeedUser("ana@example.com");
        var login = await _authService.LoginAsync(new LoginRequest("ana@example.com", Password), CancellationToken.None);
        await _authService.LogoutAsync(new RefreshTokenRequest(login.RefreshToken), CancellationToken.None);
        var savesAfterFirstLogout = _unitOfWork.SaveCount;

        await _authService.LogoutAsync(new RefreshTokenRequest(login.RefreshToken), CancellationToken.None);
        await _authService.LogoutAsync(new RefreshTokenRequest("refresh-unknown"), CancellationToken.None);

        Assert.Equal(savesAfterFirstLogout, _unitOfWork.SaveCount);
    }

    private User SeedUser(string email)
    {
        var user = new User("Ana Ruiz", email, _passwordHasher.Hash(Password), UserRole.Client);
        _users.Add(user);
        return user;
    }

    // User has no deactivation method yet (admin user management is issue #19), so tests set the flag directly.
    private static void Deactivate(User user) =>
        typeof(User).GetProperty(nameof(User.IsActive))!.SetValue(user, false);
}
