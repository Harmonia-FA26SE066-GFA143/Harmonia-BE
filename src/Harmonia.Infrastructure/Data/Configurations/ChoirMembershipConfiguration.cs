using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class ChoirMembershipConfiguration : IEntityTypeConfiguration<ChoirMembership>
{
    public void Configure(EntityTypeBuilder<ChoirMembership> builder)
    {
        builder.HasIndex(x => new { x.MemberId, x.ChoirId }).IsUnique();

        // One active choir per member (2026-11-18). IsActive is a key column, not only the filter, so EF orders the
        // UPDATE that leaves the old choir before the one that joins the new choir within a single save.
        builder.HasIndex(x => new { x.MemberId, x.IsActive })
            .IsUnique()
            .HasDatabaseName("IX_ChoirMemberships_MemberId_Active")
            .HasFilter("[IsActive] = 1");

        builder.HasOne(x => x.Member)
            .WithMany(x => x.ChoirMemberships)
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Choir)
            .WithMany(x => x.Memberships)
            .HasForeignKey(x => x.ChoirId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
