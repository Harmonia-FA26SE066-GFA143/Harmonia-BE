using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class PracticeAssignmentMaterialConfiguration : IEntityTypeConfiguration<PracticeAssignmentMaterial>
{
    public void Configure(EntityTypeBuilder<PracticeAssignmentMaterial> builder)
    {
        builder.HasIndex(x => new { x.PracticeAssignmentId, x.MaterialId }).IsUnique();

        builder.HasOne(x => x.PracticeAssignment)
            .WithMany(x => x.Materials)
            .HasForeignKey(x => x.PracticeAssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Materials are only ever soft-deleted; Restrict also keeps SQL Server clear of a second cascade path
        // (MusicMaterial -> PracticeAssignment already sets null).
        builder.HasOne(x => x.Material)
            .WithMany()
            .HasForeignKey(x => x.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
