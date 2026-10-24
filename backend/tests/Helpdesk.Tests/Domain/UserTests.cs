using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Tests.Application;

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
    public void Password_hash_can_be_replaced()
    {
        var user = new User("Ana Ruiz", "ana@example.com", "old-hash", UserRole.Client);

        user.ChangePasswordHash("new-hash");

        Assert.Equal("new-hash", user.PasswordHash);
    }

    [Fact]
    public void Replacing_the_password_hash_with_a_blank_value_is_rejected()
    {
        var user = new User("Ana Ruiz", "ana@example.com", "old-hash", UserRole.Client);

        Assert.Throws<ArgumentException>(() => user.ChangePasswordHash(" "));
        Assert.Equal("old-hash", user.PasswordHash);
    }

    // The property and the expression a query filters by must give the same answer.
    [Theory]
    [InlineData(UserRole.Agent, true, true)]
    [InlineData(UserRole.Agent, false, false)]
    [InlineData(UserRole.Client, true, false)]
    [InlineData(UserRole.Admin, true, false)]
    public void Only_an_active_agent_is_an_active_agent(UserRole role, bool isActive, bool expected)
    {
        var user = new User("Ana Ruiz", "ana@example.com", "hash", role);
        if (!isActive)
        {
            TestUsers.Deactivate(user);
        }

        Assert.Equal(expected, user.IsActiveAgent);
        Assert.Equal(expected, User.ActiveAgent.Compile()(user));
    }

    [Fact]
    public void Undefined_role_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new User("Ana Ruiz", "ana@example.com", "hash", (UserRole)7));
    }
}
