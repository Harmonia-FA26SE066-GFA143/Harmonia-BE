using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class LiturgicalSlotConfiguration : IEntityTypeConfiguration<LiturgicalSlot>
{
    public void Configure(EntityTypeBuilder<LiturgicalSlot> builder)
    {
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);

        builder.HasIndex(x => x.Name).IsUnique();
    }
}
