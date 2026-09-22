using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class PracticeAssignmentConfiguration : IEntityTypeConfiguration<PracticeAssignment>
{
    public void Configure(EntityTypeBuilder<PracticeAssignment> builder)
    {
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Instruction).HasMaxLength(1000);

        builder.HasOne(x => x.LiturgicalEvent)
            .WithMany(x => x.PracticeAssignments)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Song)
            .WithMany()
            .HasForeignKey(x => x.SongId)
            // SQL Server rejects a second SET NULL path (Song -> Material -> Assignment), so no DB-level action here.
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(x => x.Material)
            .WithMany()
            .HasForeignKey(x => x.MaterialId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
