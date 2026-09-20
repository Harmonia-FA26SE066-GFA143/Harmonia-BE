using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class SongListConfiguration : IEntityTypeConfiguration<SongList>
{
    public void Configure(EntityTypeBuilder<SongList> builder)
    {
        builder.HasIndex(x => new { x.EventId, x.Version }).IsUnique();

        builder.HasOne(x => x.LiturgicalEvent)
            .WithMany(x => x.SongLists)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Proposer)
            .WithMany()
            .HasForeignKey(x => x.ProposedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PreviousVersion)
            .WithMany()
            .HasForeignKey(x => x.PreviousVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
