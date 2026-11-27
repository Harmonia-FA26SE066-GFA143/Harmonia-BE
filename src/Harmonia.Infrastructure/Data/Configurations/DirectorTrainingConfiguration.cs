using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class DirectorTrainingConfiguration : IEntityTypeConfiguration<DirectorTraining>
{
    public void Configure(EntityTypeBuilder<DirectorTraining> builder)
    {
        builder.Property(x => x.CourseName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Organizer).HasMaxLength(200);
        builder.Property(x => x.ResultNote).HasMaxLength(500);
        builder.Property(x => x.CertificatePublicId).HasMaxLength(255);
        builder.Property(x => x.CertificateFileName).HasMaxLength(255);

        // Nominations are never deleted, so the cascade only matters for test data; courses have no life of their own.
        builder.HasOne(x => x.Nomination)
            .WithMany(x => x.Trainings)
            .HasForeignKey(x => x.NominationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
