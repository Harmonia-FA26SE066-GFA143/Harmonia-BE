using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class ChoirJoinRequestConfiguration : IEntityTypeConfiguration<ChoirJoinRequest>
{
    public void Configure(EntityTypeBuilder<ChoirJoinRequest> builder)
    {
        builder.Property(x => x.Message).HasMaxLength(500);
        builder.Property(x => x.RejectReason).HasMaxLength(500);

        // One own request Pending per member; invitations from several choirs may wait at once.
        builder.HasIndex(x => x.MemberId)
            .IsUnique()
            .HasDatabaseName("IX_ChoirJoinRequests_MemberId_PendingRequest")
            .HasFilter($"[Status] = {(int)ChoirJoinRequestStatus.Pending} AND [IsInvitation] = 0");

        // At most one Pending row per member and choir, in either direction.
        builder.HasIndex(x => new { x.MemberId, x.ChoirId })
            .IsUnique()
            .HasFilter($"[Status] = {(int)ChoirJoinRequestStatus.Pending}");

        builder.HasIndex(x => new { x.ChoirId, x.Status });

        // Restrict everywhere: a request is history and must not vanish with the member, choir or reviewer.
        builder.HasOne(x => x.Member)
            .WithMany(x => x.ChoirJoinRequests)
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Choir)
            .WithMany(x => x.JoinRequests)
            .HasForeignKey(x => x.ChoirId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReviewedBy)
            .WithMany()
            .HasForeignKey(x => x.ReviewedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
