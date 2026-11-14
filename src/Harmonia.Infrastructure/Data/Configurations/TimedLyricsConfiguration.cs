using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class TimedLyricsConfiguration : IEntityTypeConfiguration<TimedLyrics>
{
    public void Configure(EntityTypeBuilder<TimedLyrics> builder)
    {
        builder.HasIndex(x => x.AudioMaterialId).IsUnique();

        // Never queried by line, always replaced whole: one JSON column instead of a child table.
        builder.OwnsMany(x => x.Lines, lines =>
        {
            lines.ToJson();
            lines.Property(x => x.Text).HasMaxLength(500);
        });

        builder.HasOne(x => x.AudioMaterial)
            .WithOne(x => x.TimedLyrics)
            .HasForeignKey<TimedLyrics>(x => x.AudioMaterialId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: a second cascading path from MusicMaterial is rejected by SQL Server. Materials are soft-deleted
        // and MusicMaterialService removes the timing first, so this never blocks a normal delete.
        builder.HasOne(x => x.LyricsMaterial)
            .WithMany()
            .HasForeignKey(x => x.LyricsMaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
