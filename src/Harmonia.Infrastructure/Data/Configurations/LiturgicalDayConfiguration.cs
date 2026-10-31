using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class LiturgicalDayConfiguration : IEntityTypeConfiguration<LiturgicalDay>
{
    public void Configure(EntityTypeBuilder<LiturgicalDay> builder)
    {
        builder.Property(x => x.CelebrationName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Rank).HasMaxLength(50);
        builder.Property(x => x.SeasonName).HasMaxLength(50);

        builder.HasIndex(x => x.Date).IsUnique();
    }
}
