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
            new SkillCategory { Id = SkillCategoryIds.Vocal, Name = "Giọng hát" },
            new SkillCategory { Id = SkillCategoryIds.Instrument, Name = "Nhạc cụ" },
            new SkillCategory { Id = SkillCategoryIds.Solo, Name = "Hát đơn ca" },
            new SkillCategory { Id = SkillCategoryIds.Psalm, Name = "Đáp ca" },
            new SkillCategory { Id = SkillCategoryIds.ConductingSupport, Name = "Hỗ trợ điều khiển" });
    }
}
