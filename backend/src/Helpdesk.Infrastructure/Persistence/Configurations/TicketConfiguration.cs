using Helpdesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Helpdesk.Infrastructure.Persistence.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.HasKey(ticket => ticket.Id);

        builder.Property(ticket => ticket.Title).HasMaxLength(200);

        builder.Property(ticket => ticket.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(ticket => ticket.Priority)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(ticket => ticket.Category)
            .WithMany()
            .HasForeignKey(ticket => ticket.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ticket => ticket.CreatedBy)
            .WithMany()
            .HasForeignKey(ticket => ticket.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ticket => ticket.AssignedTo)
            .WithMany()
            .HasForeignKey(ticket => ticket.AssignedToId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(ticket => ticket.Comments)
            .WithOne()
            .HasForeignKey(comment => comment.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // Npgsql maps a uint row version to the xmin system column, so no physical column is created.
        builder.Property(ticket => ticket.Version).IsRowVersion();
    }
}
