using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class RehearsalSongConfiguration : IEntityTypeConfiguration<RehearsalSong>
{
    public void Configure(EntityTypeBuilder<RehearsalSong> builder)
    {
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasIndex(x => new { x.RehearsalId, x.SongId }).IsUnique();

        builder.HasOne(x => x.Rehearsal)
            .WithMany(x => x.Songs)
            .HasForeignKey(x => x.RehearsalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Song)
            .WithMany()
            .HasForeignKey(x => x.SongId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
