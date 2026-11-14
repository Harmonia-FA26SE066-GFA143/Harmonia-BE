using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class SongInstrumentRequirementConfiguration : IEntityTypeConfiguration<SongInstrumentRequirement>
{
    public void Configure(EntityTypeBuilder<SongInstrumentRequirement> builder)
    {
        builder.HasIndex(x => new { x.SongId, x.SkillId }).IsUnique();

        // Existing rows predate the count and meant one person.
        builder.Property(x => x.RequiredCount).HasDefaultValue(1);

        builder.HasOne(x => x.Song)
            .WithMany(x => x.InstrumentRequirements)
            .HasForeignKey(x => x.SongId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Skill)
            .WithMany(x => x.SongInstrumentRequirements)
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
