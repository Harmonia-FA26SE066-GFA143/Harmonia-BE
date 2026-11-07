using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class LiturgicalNameTranslationConfiguration : IEntityTypeConfiguration<LiturgicalNameTranslation>
{
    public void Configure(EntityTypeBuilder<LiturgicalNameTranslation> builder)
    {
        // Same length as LiturgicalDay.CelebrationName, which is what gets looked up.
        builder.Property(x => x.EnglishName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.VietnameseName).IsRequired().HasMaxLength(200);

        builder.HasIndex(x => new { x.Kind, x.EnglishName }).IsUnique();
    }
}
