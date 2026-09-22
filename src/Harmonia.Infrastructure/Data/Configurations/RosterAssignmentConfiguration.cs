using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class RosterAssignmentConfiguration : IEntityTypeConfiguration<RosterAssignment>
{
    public void Configure(EntityTypeBuilder<RosterAssignment> builder)
    {
        builder.HasOne(x => x.Roster)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.RosterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Member)
            .WithMany(x => x.RosterAssignments)
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Skill)
            .WithMany(x => x.RosterAssignments)
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SongListItem)
            .WithMany(x => x.RosterAssignments)
            .HasForeignKey(x => x.SongListItemId)
            // SQL Server rejects a second path (Event -> SongList -> Item -> Assignment vs Event -> Roster -> Assignment).
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
