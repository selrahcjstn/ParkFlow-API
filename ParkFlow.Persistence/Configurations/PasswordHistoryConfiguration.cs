using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations;

public class PasswordHistoryConfiguration : IEntityTypeConfiguration<PasswordHistory>
{
    public void Configure(EntityTypeBuilder<PasswordHistory> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(e => e.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.Property(e => e.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        entity.HasOne(e => e.UserAccount)
            .WithMany(u => u.PasswordHistories)
            .HasForeignKey(e => e.UserAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
