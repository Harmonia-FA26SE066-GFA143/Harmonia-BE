using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class MassScheduleConfiguration : IEntityTypeConfiguration<MassSchedule>
{
    public void Configure(EntityTypeBuilder<MassSchedule> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(200);

        builder.HasIndex(x => new { x.DayOfWeek, x.Time, x.LocationId }).IsUnique();

        builder.HasOne(x => x.MassType)
            .WithMany(x => x.MassSchedules)
            .HasForeignKey(x => x.MassTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Location)
            .WithMany(x => x.MassSchedules)
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
