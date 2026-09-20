using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class PracticeAssignmentTargetConfiguration : IEntityTypeConfiguration<PracticeAssignmentTarget>
{
    public void Configure(EntityTypeBuilder<PracticeAssignmentTarget> builder)
    {
        builder.HasOne(x => x.PracticeAssignment)
            .WithMany(x => x.Targets)
            .HasForeignKey(x => x.PracticeAssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Member)
            .WithMany()
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Skill)
            .WithMany()
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
