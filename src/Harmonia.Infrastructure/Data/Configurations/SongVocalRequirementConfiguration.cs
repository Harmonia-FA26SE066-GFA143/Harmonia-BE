using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class SongVocalRequirementConfiguration : IEntityTypeConfiguration<SongVocalRequirement>
{
    public void Configure(EntityTypeBuilder<SongVocalRequirement> builder)
    {
        builder.HasIndex(x => new { x.SongId, x.SkillId }).IsUnique();

        builder.HasOne(x => x.Song)
            .WithMany(x => x.VocalRequirements)
            .HasForeignKey(x => x.SongId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Skill)
            .WithMany(x => x.SongVocalRequirements)
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
