using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class RehearsalConfiguration : IEntityTypeConfiguration<Rehearsal>
{
    public void Configure(EntityTypeBuilder<Rehearsal> builder)
    {
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasOne(x => x.LiturgicalEvent)
            .WithMany(x => x.Rehearsals)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Location)
            .WithMany(x => x.Rehearsals)
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
