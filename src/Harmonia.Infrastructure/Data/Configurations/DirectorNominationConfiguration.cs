using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class DirectorNominationConfiguration : IEntityTypeConfiguration<DirectorNomination>
{
    public void Configure(EntityTypeBuilder<DirectorNomination> builder)
    {
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.ReviewNote).HasMaxLength(500);
        builder.Property(x => x.TrainingWaiverReason).HasMaxLength(500);

        builder.HasIndex(x => new { x.MemberId, x.Status });

        // Restrict everywhere: a nomination is history and must not vanish with the member, user or choir.
        builder.HasOne(x => x.Member)
            .WithMany(x => x.DirectorNominations)
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.NominatedBy)
            .WithMany()
            .HasForeignKey(x => x.NominatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReviewedBy)
            .WithMany()
            .HasForeignKey(x => x.ReviewedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProposedChoir)
            .WithMany()
            .HasForeignKey(x => x.ProposedChoirId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AppointedChoir)
            .WithMany()
            .HasForeignKey(x => x.AppointedChoirId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
