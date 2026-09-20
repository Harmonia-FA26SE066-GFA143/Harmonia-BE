using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class LiturgicalSeasonConfiguration : IEntityTypeConfiguration<LiturgicalSeason>
{
    public void Configure(EntityTypeBuilder<LiturgicalSeason> builder)
    {
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ColorHex).HasMaxLength(7);
    }
}
