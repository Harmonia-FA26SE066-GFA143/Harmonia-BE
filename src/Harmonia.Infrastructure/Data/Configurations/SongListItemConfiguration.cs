using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class SongListItemConfiguration : IEntityTypeConfiguration<SongListItem>
{
    public void Configure(EntityTypeBuilder<SongListItem> builder)
    {
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasOne(x => x.SongList)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.SongListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Song)
            .WithMany(x => x.SongListItems)
            .HasForeignKey(x => x.SongId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Slot)
            .WithMany(x => x.SongListItems)
            .HasForeignKey(x => x.SlotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
