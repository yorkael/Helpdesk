using Helpdesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Helpdesk.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Name).HasMaxLength(100);

        builder.Property(user => user.Email).HasMaxLength(256);
        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.PasswordHash).HasMaxLength(500);

        builder.Property(user => user.Role)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Sentinel true: EF omits the column only when the value matches the database default,
        // so an inactive user is still persisted as false.
        builder.Property(user => user.IsActive)
            .HasDefaultValue(true)
            .HasSentinel(true);
    }
}
