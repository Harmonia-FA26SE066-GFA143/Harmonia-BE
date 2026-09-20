using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class DirectorNoteConfiguration : IEntityTypeConfiguration<DirectorNote>
{
    public void Configure(EntityTypeBuilder<DirectorNote> builder)
    {
        builder.Property(x => x.Content).IsRequired().HasMaxLength(2000);

        builder.HasOne(x => x.Week)
            .WithMany(x => x.DirectorNotes)
            .HasForeignKey(x => x.WeekId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.LiturgicalEvent)
            .WithMany(x => x.DirectorNotes)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.FromUser)
            .WithMany()
            .HasForeignKey(x => x.FromUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToUser)
            .WithMany()
            .HasForeignKey(x => x.ToUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
