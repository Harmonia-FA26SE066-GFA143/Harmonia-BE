using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class MemberRegistrationConfiguration : IEntityTypeConfiguration<MemberRegistration>
{
    public void Configure(EntityTypeBuilder<MemberRegistration> builder)
    {
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.SaintName).HasMaxLength(50);
        builder.Property(x => x.Note).HasMaxLength(500);

        // Not unique: two submissions racing on one email would otherwise turn an anonymous form into a 500.
        builder.HasIndex(x => x.Email);
    }
}
