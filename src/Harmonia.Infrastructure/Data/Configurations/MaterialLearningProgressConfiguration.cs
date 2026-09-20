using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class MaterialLearningProgressConfiguration : IEntityTypeConfiguration<MaterialLearningProgress>
{
    public void Configure(EntityTypeBuilder<MaterialLearningProgress> builder)
    {
        builder.HasIndex(x => new { x.MemberId, x.MaterialId }).IsUnique();

        builder.HasOne(x => x.Member)
            .WithMany(x => x.LearningProgresses)
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Material)
            .WithMany(x => x.LearningProgresses)
            .HasForeignKey(x => x.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
