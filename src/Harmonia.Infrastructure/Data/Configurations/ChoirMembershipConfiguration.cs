using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class ChoirMembershipConfiguration : IEntityTypeConfiguration<ChoirMembership>
{
    public void Configure(EntityTypeBuilder<ChoirMembership> builder)
    {
        builder.HasIndex(x => new { x.MemberId, x.ChoirId }).IsUnique();

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
