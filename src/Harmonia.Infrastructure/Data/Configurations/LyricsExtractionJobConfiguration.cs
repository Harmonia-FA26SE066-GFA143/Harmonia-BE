using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class LyricsExtractionJobConfiguration : IEntityTypeConfiguration<LyricsExtractionJob>
{
    public void Configure(EntityTypeBuilder<LyricsExtractionJob> builder)
    {
        builder.Property(x => x.ErrorCode).HasMaxLength(100);

        builder.HasIndex(x => new { x.MusicMaterialId, x.Status });

        builder.HasOne(x => x.MusicMaterial)
            .WithMany()
            .HasForeignKey(x => x.MusicMaterialId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
