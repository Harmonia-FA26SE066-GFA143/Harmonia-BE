using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class RehearsalAttendanceConfiguration : IEntityTypeConfiguration<RehearsalAttendance>
{
    public void Configure(EntityTypeBuilder<RehearsalAttendance> builder)
    {
        builder.HasIndex(x => new { x.RehearsalId, x.MemberId }).IsUnique();

        builder.HasOne(x => x.Rehearsal)
            .WithMany(x => x.Attendances)
            .HasForeignKey(x => x.RehearsalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Member)
            .WithMany(x => x.RehearsalAttendances)
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Checker)
            .WithMany()
            .HasForeignKey(x => x.CheckedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
