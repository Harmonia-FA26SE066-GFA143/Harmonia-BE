using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.Property(x => x.Key).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Value).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.Description).HasMaxLength(300);

        builder.HasIndex(x => x.Key).IsUnique();
    }
}
