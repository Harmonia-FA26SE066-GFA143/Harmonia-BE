using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class MusicMaterialConfiguration : IEntityTypeConfiguration<MusicMaterial>
{
    public void Configure(EntityTypeBuilder<MusicMaterial> builder)
    {
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.FileUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(200);

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
