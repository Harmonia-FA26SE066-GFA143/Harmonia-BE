using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class LiturgicalEventConfiguration : IEntityTypeConfiguration<LiturgicalEvent>
{
    public void Configure(EntityTypeBuilder<LiturgicalEvent> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.SpecialRequirements).HasMaxLength(1000);

        builder.HasIndex(x => new { x.EventDate, x.Time, x.LocationId }).IsUnique();

        builder.HasOne(x => x.Week)
            .WithMany(x => x.LiturgicalEvents)
            .HasForeignKey(x => x.WeekId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MassType)
            .WithMany(x => x.LiturgicalEvents)
            .HasForeignKey(x => x.MassTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CeremonyType)
            .WithMany(x => x.LiturgicalEvents)
            .HasForeignKey(x => x.CeremonyTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Category)
            .WithMany(x => x.LiturgicalEvents)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Location)
            .WithMany(x => x.LiturgicalEvents)
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
