using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class LiturgicalSeasonConfiguration : IEntityTypeConfiguration<LiturgicalSeason>
{
    public void Configure(EntityTypeBuilder<LiturgicalSeason> builder)
    {
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ColorHex).HasMaxLength(7);
        builder.Property(x => x.CalendarKey).HasMaxLength(20);

        // One active season per calendar key, so an event's day resolves to a single row.
        builder.HasIndex(x => x.CalendarKey)
            .IsUnique()
            .HasFilter("[CalendarKey] IS NOT NULL AND [IsActive] = 1");
    }
}
