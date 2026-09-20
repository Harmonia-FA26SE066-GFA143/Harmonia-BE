using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class SongListReviewConfiguration : IEntityTypeConfiguration<SongListReview>
{
    public void Configure(EntityTypeBuilder<SongListReview> builder)
    {
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.SongList)
            .WithMany(x => x.Reviews)
            .HasForeignKey(x => x.SongListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Reviewer)
            .WithMany()
            .HasForeignKey(x => x.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
