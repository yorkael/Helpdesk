using Helpdesk.Domain.Enums;

namespace Helpdesk.Domain.Entities;

public class User
{
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

    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = Guard.NotBlank(passwordHash, nameof(passwordHash));
    }
}
