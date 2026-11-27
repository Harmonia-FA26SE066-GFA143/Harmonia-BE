using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class MusicMaterialConfiguration : IEntityTypeConfiguration<MusicMaterial>
{
    public void Configure(EntityTypeBuilder<MusicMaterial> builder)
    {
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.FilePublicId).HasMaxLength(255);
        builder.Property(x => x.FileName).HasMaxLength(200);
        builder.Property(x => x.PlaybackAudioPublicId).HasMaxLength(255);
        builder.Property(x => x.PlaybackNotesPublicId).HasMaxLength(255);
        builder.Property(x => x.ScoreSha256).HasMaxLength(64);
        builder.Property(x => x.PlaybackAudioSha256).HasMaxLength(64);
        builder.Property(x => x.PlaybackNotesSha256).HasMaxLength(64);
        // Existing rows become revision 1 when the column is added.
        builder.Property(x => x.FileRevision).HasDefaultValue(1);

        builder.HasOne(x => x.Song)
            .WithMany(x => x.MusicMaterials)
            .HasForeignKey(x => x.SongId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TargetSkill)
            .WithMany()
            .HasForeignKey(x => x.TargetSkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
