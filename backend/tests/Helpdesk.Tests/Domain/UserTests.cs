using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;

namespace Helpdesk.Tests.Domain;

public class UserTests
{
    [Fact]
    public void New_user_is_active()
    {
        var user = new User("Ana Ruiz", "ana@example.com", "hash", UserRole.Client);

        Assert.True(user.IsActive);
    }

    [Fact]
    public void Email_is_trimmed_and_lowercased()
    {
        var user = new User("Ana Ruiz", "  Ana.Ruiz@Example.COM ", "hash", UserRole.Agent);

        Assert.Equal("ana.ruiz@example.com", user.Email);
    }

    [Fact]
    public void Blank_password_hash_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new User("Ana Ruiz", "ana@example.com", " ", UserRole.Client));
    }

    [Fact]
    public void Undefined_role_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new User("Ana Ruiz", "ana@example.com", "hash", (UserRole)7));
    }
}
