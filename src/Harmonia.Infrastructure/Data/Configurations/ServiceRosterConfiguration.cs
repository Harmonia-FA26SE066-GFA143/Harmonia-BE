using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class ServiceRosterConfiguration : IEntityTypeConfiguration<ServiceRoster>
{
    public void Configure(EntityTypeBuilder<ServiceRoster> builder)
    {
        builder.HasIndex(x => x.EventId).IsUnique();

        builder.HasOne(x => x.LiturgicalEvent)
            .WithOne(x => x.ServiceRoster)
            .HasForeignKey<ServiceRoster>(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Generator)
            .WithMany()
            .HasForeignKey(x => x.GeneratedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Finalizer)
            .WithMany()
            .HasForeignKey(x => x.FinalizedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
