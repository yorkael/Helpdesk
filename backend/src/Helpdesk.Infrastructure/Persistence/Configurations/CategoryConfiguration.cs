using Helpdesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Helpdesk.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name).HasMaxLength(100);
        builder.HasIndex(category => category.Name).IsUnique();

        // Fixed ids keep the seed stable across migrations; admins will manage categories in #19.
        builder.HasData(
            new { Id = new Guid("df4f78ca-b46c-4c69-b573-5074c623ba1b"), Name = "General" },
            new { Id = new Guid("79377fbb-510c-4370-81b2-5cc7418ce2bc"), Name = "Technical issue" },
            new { Id = new Guid("ed1c5f15-7408-4ba8-a984-70410da0903f"), Name = "Billing" },
            new { Id = new Guid("6f10028c-f53c-4210-8d8f-b5cd70b0f1cf"), Name = "Account" });
    }
}
