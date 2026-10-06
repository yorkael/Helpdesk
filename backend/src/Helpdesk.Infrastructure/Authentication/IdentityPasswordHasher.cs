using System.Security.Cryptography;
using Helpdesk.Application.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Helpdesk.Infrastructure.Authentication;

/// <summary>
/// PBKDF2-HMAC-SHA512 with a random 128-bit salt (ASP.NET Core Identity V3 format). The hash stores
/// its own parameters, so raising <see cref="IterationCount"/> later makes old hashes report a rehash.
/// </summary>
internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    // OWASP Password Storage Cheat Sheet recommendation for PBKDF2-HMAC-SHA512.
    public const int IterationCount = 210_000;

    // PasswordHasher<TUser> takes a user argument but does not use it.
    private static readonly object NoUser = new();

    private readonly PasswordHasher<object> _hasher =
        new(Options.Create(new PasswordHasherOptions { IterationCount = IterationCount }));

    private readonly string _dummyHash;

    public IdentityPasswordHasher()
    {
        _dummyHash = _hasher.HashPassword(NoUser, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public string Hash(string password) => _hasher.HashPassword(NoUser, password);

    public PasswordVerification Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(NoUser, passwordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordVerification.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessRehashNeeded,
            _ => PasswordVerification.Failed
        };

    public void SimulateVerification(string password) => _hasher.VerifyHashedPassword(NoUser, _dummyHash, password);
}
