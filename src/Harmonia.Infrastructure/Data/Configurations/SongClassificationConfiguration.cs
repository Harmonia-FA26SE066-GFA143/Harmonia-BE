using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class SongClassificationConfiguration : IEntityTypeConfiguration<SongClassification>
{
    public void Configure(EntityTypeBuilder<SongClassification> builder)
    {
        builder.HasIndex(x => new { x.SongId, x.TargetType, x.TargetId }).IsUnique();

        // TargetId is polymorphic so no foreign key is possible; index it for reverse lookups.
        builder.HasIndex(x => new { x.TargetType, x.TargetId });

        builder.HasOne(x => x.Song)
            .WithMany(x => x.Classifications)
            .HasForeignKey(x => x.SongId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
