using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations;

public class AuthIdentityConfiguration : IEntityTypeConfiguration<AuthIdentity>
{
    public void Configure(EntityTypeBuilder<AuthIdentity> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(e => e.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.Property(e => e.Provider)
            .IsRequired();

        entity.Property(e => e.Email)
            .HasMaxLength(320);

        entity.Property(e => e.ProviderId)
            .HasMaxLength(200);

        entity.Property(e => e.PasswordHash)
            .HasMaxLength(512);

        entity.Property(e => e.IsVerified)
            .HasDefaultValue(false);

        entity.HasOne(e => e.UserAccount)
            .WithMany(u => u.AuthIdentities)
            .HasForeignKey(e => e.UserAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => new { e.Provider, e.ProviderId })
            .IsUnique()
            .HasFilter("\"ProviderId\" IS NOT NULL");

        entity.HasIndex(e => e.Email)
            .IsUnique()
            .HasFilter("\"Email\" IS NOT NULL");
    }
}
