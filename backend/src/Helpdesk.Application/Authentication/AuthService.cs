using FluentValidation;
using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Common;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;

namespace Helpdesk.Application.Authentication;

public sealed class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer accessTokenIssuer,
    IRefreshTokenIssuer refreshTokenIssuer,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<RefreshTokenRequest> refreshTokenValidator,
    TimeProvider timeProvider)
{
    public async Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var email = NormalizeEmail(request.Email);
        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            throw new EmailAlreadyRegisteredException();
        }

        // Public registration always creates a Client; staff accounts are created by an admin.
        var user = new User(request.Name, email, passwordHasher.Hash(request.Password), UserRole.Client);
        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserResponse(user.Id, user.Name, user.Email, user.Role.ToString());
    }

    public async Task<AuthTokensResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await users.GetByEmailAsync(NormalizeEmail(request.Email), cancellationToken);
        if (user is null)
        {
            passwordHasher.SimulateVerification(request.Password);
            throw new InvalidCredentialsException();
        }

        // The password is checked before IsActive so every rejection costs the same hashing time.
        var verification = passwordHasher.Verify(user.PasswordHash, request.Password);
        if (verification == PasswordVerification.Failed || !user.IsActive)
        {
            throw new InvalidCredentialsException();
        }

        if (verification == PasswordVerification.SuccessRehashNeeded)
        {
            user.ChangePasswordHash(passwordHasher.Hash(request.Password));
        }

        var now = timeProvider.GetUtcNow();
        var refreshToken = refreshTokenIssuer.Issue(user.Id, now);
        refreshTokens.Add(refreshToken.Entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CreateTokensResponse(user, refreshToken, now);
    }

    public async Task<AuthTokensResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await refreshTokenValidator.ValidateAndThrowAsync(request, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var storedToken = await refreshTokens.GetByHashAsync(refreshTokenIssuer.Hash(request.RefreshToken), cancellationToken)
            ?? throw new InvalidRefreshTokenException();

        if (storedToken.RevokedAt is not null)
        {
            // A rotated token was presented again: it may have been stolen, so end every session of the user.
            await RevokeAllActiveTokensAsync(storedToken.UserId, now, cancellationToken);
            throw new InvalidRefreshTokenException();
        }

        if (!storedToken.IsActive(now))
        {
            throw new InvalidRefreshTokenException();
        }

        var user = await users.GetByIdAsync(storedToken.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new InvalidRefreshTokenException();
        }

        var replacement = refreshTokenIssuer.Issue(user.Id, now);
        storedToken.Revoke(now, replacement.Entity.Id);
        refreshTokens.Add(replacement.Entity);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another request rotated this token first. Rejecting is enough; it is not treated as reuse,
            // because two tabs refreshing at once is a legitimate race.
            throw new InvalidRefreshTokenException();
        }

        return CreateTokensResponse(user, replacement, now);
    }

    /// <summary>
    /// Idempotent: an unknown or already revoked token is ignored, so the caller learns nothing about it.
    /// </summary>
    public async Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await refreshTokenValidator.ValidateAndThrowAsync(request, cancellationToken);

        var storedToken = await refreshTokens.GetByHashAsync(refreshTokenIssuer.Hash(request.RefreshToken), cancellationToken);
        if (storedToken is null || storedToken.RevokedAt is not null)
        {
            return;
        }

        storedToken.Revoke(timeProvider.GetUtcNow());

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Revoked or rotated concurrently; either way it can no longer be used.
        }
    }

    private async Task RevokeAllActiveTokensAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var token in await refreshTokens.GetActiveByUserAsync(userId, now, cancellationToken))
        {
            token.Revoke(now);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Known limitation: the save is atomic, so if another request changes one of these tokens at the
            // same moment, none of them is revoked. The reused token is still rejected; no retry by design.
        }
    }

    private AuthTokensResponse CreateTokensResponse(User user, IssuedRefreshToken refreshToken, DateTimeOffset now)
    {
        var accessToken = accessTokenIssuer.Issue(user, now);
        return new AuthTokensResponse(
            accessToken.Value,
            accessToken.ExpiresAt,
            refreshToken.Token,
            refreshToken.Entity.ExpiresAt);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
