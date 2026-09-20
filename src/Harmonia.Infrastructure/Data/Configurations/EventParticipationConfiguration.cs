using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class EventParticipationConfiguration : IEntityTypeConfiguration<EventParticipation>
{
    public void Configure(EntityTypeBuilder<EventParticipation> builder)
    {
        builder.Property(x => x.Note).HasMaxLength(300);

        builder.HasIndex(x => new { x.EventId, x.MemberId }).IsUnique();

        builder.HasOne(x => x.LiturgicalEvent)
            .WithMany(x => x.EventParticipations)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Member)
            .WithMany(x => x.EventParticipations)
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
