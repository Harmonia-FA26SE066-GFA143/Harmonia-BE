using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class ReportExportConfiguration : IEntityTypeConfiguration<ReportExport>
{
    public void Configure(EntityTypeBuilder<ReportExport> builder)
    {
        builder.Property(x => x.Parameters).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.FileUrl).IsRequired().HasMaxLength(500);

        builder.HasOne(x => x.Exporter)
            .WithMany()
            .HasForeignKey(x => x.ExportedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
