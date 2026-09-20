using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class SongConfiguration : IEntityTypeConfiguration<Song>
{
    public void Configure(EntityTypeBuilder<Song> builder)
    {
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Composer).HasMaxLength(150);
        builder.Property(x => x.Lyricist).HasMaxLength(150);
        builder.Property(x => x.MusicalKey).HasMaxLength(10);
        builder.Property(x => x.Tempo).HasMaxLength(50);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => x.Title);
    }
}
