using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class PracticeFeedbackConfiguration : IEntityTypeConfiguration<PracticeFeedback>
{
    public void Configure(EntityTypeBuilder<PracticeFeedback> builder)
    {
        builder.Property(x => x.Comment).HasMaxLength(1000);

        builder.HasOne(x => x.Submission)
            .WithMany(x => x.Feedbacks)
            .HasForeignKey(x => x.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Reviewer)
            .WithMany()
            .HasForeignKey(x => x.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
