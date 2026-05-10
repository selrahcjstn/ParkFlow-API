using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ParkFlow.Persistence.Configurations;

public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> entity)
    {

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(e => e.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.Property(e => e.Email)
            .IsRequired();

        entity.Property(e => e.PasswordHash)
            .IsRequired();

        entity.Property(e => e.PhoneNumber)
            .IsRequired();

        entity.Property(e => e.PasswordResetTokenHash)
            .HasMaxLength(128)
            .IsRequired(false);

        entity.Property(e => e.PasswordResetTokenExpiresAt)
            .IsRequired(false);

    }
}
