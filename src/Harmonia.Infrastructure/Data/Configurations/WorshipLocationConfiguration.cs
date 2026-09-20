using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class WorshipLocationConfiguration : IEntityTypeConfiguration<WorshipLocation>
{
    public void Configure(EntityTypeBuilder<WorshipLocation> builder)
    {
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Address).HasMaxLength(300);

        builder.HasIndex(x => x.Name).IsUnique();
    }
}
