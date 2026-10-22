using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class LiturgicalEventChoirConfiguration : IEntityTypeConfiguration<LiturgicalEventChoir>
{
    public void Configure(EntityTypeBuilder<LiturgicalEventChoir> builder)
    {
        builder.HasIndex(x => new { x.EventId, x.ChoirId }).IsUnique();

        builder.HasOne(x => x.LiturgicalEvent)
            .WithMany(x => x.EventChoirs)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Choir)
            .WithMany(x => x.EventChoirs)
            .HasForeignKey(x => x.ChoirId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
