using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class PracticeSubmissionConfiguration : IEntityTypeConfiguration<PracticeSubmission>
{
    public void Configure(EntityTypeBuilder<PracticeSubmission> builder)
    {
        builder.Property(x => x.AudioPublicId).IsRequired().HasMaxLength(255);

        builder.HasIndex(x => new { x.PracticeAssignmentId, x.MemberId, x.AttemptNo }).IsUnique();

        builder.HasOne(x => x.PracticeAssignment)
            .WithMany(x => x.Submissions)
            .HasForeignKey(x => x.PracticeAssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Member)
            .WithMany(x => x.PracticeSubmissions)
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
