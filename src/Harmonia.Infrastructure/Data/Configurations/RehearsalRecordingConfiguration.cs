using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class RehearsalRecordingConfiguration : IEntityTypeConfiguration<RehearsalRecording>
{
    public void Configure(EntityTypeBuilder<RehearsalRecording> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.AudioPublicId).IsRequired().HasMaxLength(255);
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(255);

        builder.HasIndex(x => new { x.RehearsalId, x.CreatedAt });

        builder.HasOne(x => x.Rehearsal)
            .WithMany(x => x.Recordings)
            .HasForeignKey(x => x.RehearsalId)
            .OnDelete(DeleteBehavior.Cascade);

        // Songs are retired, not deleted, so a recording never blocks anything in practice.
        builder.HasOne(x => x.Song)
            .WithMany()
            .HasForeignKey(x => x.SongId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
