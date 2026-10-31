using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class LiturgicalEventChoirConfiguration : IEntityTypeConfiguration<LiturgicalEventChoir>
{
    public void Configure(EntityTypeBuilder<LiturgicalEventChoir> builder)
    {
        // No filtered unique index on IsLead: moving the lead between two rows in one save could trip it mid-batch.
        // ChoirService.UpdateEventChoirsAsync is the only writer and keeps a single lead.
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
