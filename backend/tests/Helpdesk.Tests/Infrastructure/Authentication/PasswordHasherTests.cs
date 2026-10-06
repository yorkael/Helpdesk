using System.Buffers.Binary;
using Helpdesk.Application.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Helpdesk.Tests.Infrastructure.Authentication;

public class PasswordHasherTests
{
    private const string Password = "correct horse battery";

    // Value of KeyDerivationPrf.HMACSHA512 in the stored hash.
    private const uint KeyDerivationPrfHmacSha512 = 2;

    private readonly IPasswordHasher _hasher =
        TestConfiguration.CreateInfrastructureServices().GetRequiredService<IPasswordHasher>();

    [Fact]
    public void Hash_verifies_only_the_original_password()
    {
        var hash = _hasher.Hash(Password);

        Assert.Equal(PasswordVerification.Success, _hasher.Verify(hash, Password));
        Assert.Equal(PasswordVerification.Failed, _hasher.Verify(hash, "Correct horse battery"));
    }

    [Fact]
    public void Same_password_gets_a_different_hash_each_time()
    {
        Assert.NotEqual(_hasher.Hash(Password), _hasher.Hash(Password));
    }

    [Fact]
    public void Hash_does_not_contain_the_password()
    {
        Assert.DoesNotContain(Password, _hasher.Hash(Password));
    }

    [Fact]
    public void Hash_uses_pbkdf2_sha512_with_210000_iterations_and_a_128_bit_salt()
    {
        // Identity V3 layout: format marker, PRF, iteration count and salt length, big-endian.
        var bytes = Convert.FromBase64String(_hasher.Hash(Password));

        Assert.Equal(0x01, bytes[0]);
        Assert.Equal(KeyDerivationPrfHmacSha512,BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1)));
        Assert.Equal(210_000u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5)));
        Assert.Equal(16u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(9)));
    }

    [Fact]
    public void Hash_with_fewer_iterations_asks_for_a_rehash()
    {
        var weakerHasher = new PasswordHasher<object>(
            Options.Create(new PasswordHasherOptions { IterationCount = 100_000 }));
        var oldHash = weakerHasher.HashPassword(new object(), Password);

        Assert.Equal(PasswordVerification.SuccessRehashNeeded, _hasher.Verify(oldHash, Password));
    }
}
