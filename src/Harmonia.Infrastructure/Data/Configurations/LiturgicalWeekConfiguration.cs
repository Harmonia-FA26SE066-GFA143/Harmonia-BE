using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class LiturgicalWeekConfiguration : IEntityTypeConfiguration<LiturgicalWeek>
{
    public void Configure(EntityTypeBuilder<LiturgicalWeek> builder)
    {
        builder.HasIndex(x => x.WeekStartDate).IsUnique();

        builder.HasOne(x => x.LiturgicalSeason)
            .WithMany(x => x.LiturgicalWeeks)
            .HasForeignKey(x => x.LiturgicalSeasonId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
