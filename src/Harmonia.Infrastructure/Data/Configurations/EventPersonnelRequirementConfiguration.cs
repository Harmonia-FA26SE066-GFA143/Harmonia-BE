using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class EventPersonnelRequirementConfiguration : IEntityTypeConfiguration<EventPersonnelRequirement>
{
    public void Configure(EntityTypeBuilder<EventPersonnelRequirement> builder)
    {
        builder.HasIndex(x => new { x.EventId, x.SkillId }).IsUnique();

        builder.HasOne(x => x.LiturgicalEvent)
            .WithMany(x => x.PersonnelRequirements)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Skill)
            .WithMany()
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
