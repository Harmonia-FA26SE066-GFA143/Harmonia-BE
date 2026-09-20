using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Content).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.ReferenceType).HasMaxLength(50);

        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
    }
}
