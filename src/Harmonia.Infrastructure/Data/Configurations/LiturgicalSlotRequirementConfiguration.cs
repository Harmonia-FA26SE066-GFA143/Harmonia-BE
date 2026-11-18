using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class LiturgicalSlotRequirementConfiguration : IEntityTypeConfiguration<LiturgicalSlotRequirement>
{
    public void Configure(EntityTypeBuilder<LiturgicalSlotRequirement> builder)
    {
        builder.HasIndex(x => new { x.SlotId, x.SkillId }).IsUnique();

        builder.HasOne(x => x.Slot)
            .WithMany(x => x.Requirements)
            .HasForeignKey(x => x.SlotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Skill)
            .WithMany()
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
