using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonia.Infrastructure.Data.Configurations;

public class MemberProfileConfiguration : IEntityTypeConfiguration<MemberProfile>
{
    public void Configure(EntityTypeBuilder<MemberProfile> builder)
    {
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Phone).HasMaxLength(20);

        builder.HasIndex(x => x.UserId).IsUnique();

        builder.HasOne(x => x.User)
            .WithOne(x => x.MemberProfile)
            .HasForeignKey<MemberProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
