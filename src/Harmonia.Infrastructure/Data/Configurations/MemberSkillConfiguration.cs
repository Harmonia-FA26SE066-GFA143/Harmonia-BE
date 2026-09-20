using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class MemberSkillConfiguration : IEntityTypeConfiguration<MemberSkill>
{
    public void Configure(EntityTypeBuilder<MemberSkill> builder)
    {
        builder.Property(x => x.RejectReason).HasMaxLength(500);

        builder.HasIndex(x => new { x.MemberId, x.SkillId }).IsUnique();

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
