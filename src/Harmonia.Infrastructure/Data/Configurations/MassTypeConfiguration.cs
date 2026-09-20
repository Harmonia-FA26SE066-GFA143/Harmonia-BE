using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class MassTypeConfiguration : IEntityTypeConfiguration<MassType>
{
    public void Configure(EntityTypeBuilder<MassType> builder)
    {
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(300);

        builder.HasIndex(x => x.Name).IsUnique();
    }
}
