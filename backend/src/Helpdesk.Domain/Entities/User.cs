using System.Linq.Expressions;
using Helpdesk.Domain.Enums;

namespace Helpdesk.Domain.Entities;

public class User
{
    /// <summary>Who a ticket can be assigned to. An expression, so a database query can filter by the same rule.</summary>
    public static readonly Expression<Func<User, bool>> ActiveAgent =
        user => user.IsActive && user.Role == UserRole.Agent;

    // Declared after ActiveAgent: static fields are initialized in order.
    private static readonly Func<User, bool> IsActiveAgentCheck = ActiveAgent.Compile();

    public User(string name, string email, string passwordHash, UserRole role)
    {
        Id = Guid.CreateVersion7();
        Name = Guard.NotBlank(name, nameof(name));
        Email = Guard.NotBlank(email, nameof(email)).ToLowerInvariant();
        PasswordHash = Guard.NotBlank(passwordHash, nameof(passwordHash));
        Role = Guard.Defined(role, nameof(role));
        IsActive = true;
    }

    private User()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsActiveAgent => IsActiveAgentCheck(this);

    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = Guard.NotBlank(passwordHash, nameof(passwordHash));
    }
}
