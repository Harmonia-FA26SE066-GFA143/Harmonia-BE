using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class SkillCategoryConfiguration : IEntityTypeConfiguration<SkillCategory>
{
    public void Configure(EntityTypeBuilder<SkillCategory> builder)
    {
        builder.Property(x => x.Name).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(300);

        builder.HasIndex(x => x.Name).IsUnique();
    }
}
