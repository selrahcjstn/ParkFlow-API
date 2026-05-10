using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ParkFlow.Persistence.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(e => e.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.Property(e => e.FirstName)
            .IsRequired();

        entity.Property(e => e.FirstName)
            .HasMaxLength(100);

        entity.Property(e => e.LastName)
            .IsRequired();

        entity.Property(e => e.LastName)
            .HasMaxLength(100);

        entity.Property(e => e.ProfilePictureUrl)
            .HasMaxLength(2048);

        // One-to-One Relationship
        entity.HasOne(e => e.UserAccount)
            .WithOne()
            .HasForeignKey<UserProfile>(e => e.UserAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.UserAccountId)
            .IsUnique();
    }
}