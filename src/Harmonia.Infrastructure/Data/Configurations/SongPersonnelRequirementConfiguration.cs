using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class SongPersonnelRequirementConfiguration : IEntityTypeConfiguration<SongPersonnelRequirement>
{
    public void Configure(EntityTypeBuilder<SongPersonnelRequirement> builder)
    {
        builder.HasIndex(x => new { x.SongListItemId, x.SkillId }).IsUnique();

        builder.HasOne(x => x.SongListItem)
            .WithMany(x => x.PersonnelRequirements)
            .HasForeignKey(x => x.SongListItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Skill)
            .WithMany(x => x.SongPersonnelRequirements)
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
