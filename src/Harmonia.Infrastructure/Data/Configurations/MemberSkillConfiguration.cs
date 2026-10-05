using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class MemberSkillConfiguration : IEntityTypeConfiguration<MemberSkill>
{
    public void Configure(EntityTypeBuilder<MemberSkill> builder)
    {
        builder.Property(x => x.RejectReason).HasMaxLength(500);

        // One live (Pending or Approved) declaration per member and skill; rejected rows stay as
        // history, so the member can declare the same skill again after a rejection.
        builder.HasIndex(x => new { x.MemberId, x.SkillId })
            .IsUnique()
            .HasFilter($"[Status] <> {(int)ApprovalStatus.Rejected}");

        builder.HasOne(x => x.Member)
            .WithMany(x => x.MemberSkills)
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Skill)
            .WithMany(x => x.MemberSkills)
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Approver)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
