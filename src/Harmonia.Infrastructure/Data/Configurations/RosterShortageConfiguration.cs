using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class RosterShortageConfiguration : IEntityTypeConfiguration<RosterShortage>
{
    public void Configure(EntityTypeBuilder<RosterShortage> builder)
    {
        builder.HasOne(x => x.Roster)
            .WithMany(x => x.Shortages)
            .HasForeignKey(x => x.RosterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Skill)
            .WithMany(x => x.RosterShortages)
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
