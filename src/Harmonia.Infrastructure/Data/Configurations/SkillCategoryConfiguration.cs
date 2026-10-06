using Harmonia.Domain.Common;
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

        // Fixed rows from 02-naming.md; code refers to them through SkillCategoryIds.
        builder.HasData(
            new SkillCategory { Id = SkillCategoryIds.Vocal, Name = "Vocal" },
            new SkillCategory { Id = SkillCategoryIds.Instrument, Name = "Instrument" },
            new SkillCategory { Id = SkillCategoryIds.Solo, Name = "Solo" },
            new SkillCategory { Id = SkillCategoryIds.Psalm, Name = "Psalm" },
            new SkillCategory { Id = SkillCategoryIds.ConductingSupport, Name = "Conducting support" });
    }
}
