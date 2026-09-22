using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations;

public class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserAccountId)
            .IsRequired();

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(n => n.Body)
            .IsRequired();

        builder.Property(n => n.Type)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(n => n.Subtitle)
            .HasMaxLength(256);

        builder.Property(n => n.ReferenceCode)
            .HasMaxLength(128);

        builder.Property(n => n.VehiclePlate)
            .HasMaxLength(64);

        builder.Property(n => n.ActionRoute)
            .HasMaxLength(256);

        builder.Property(n => n.ActionText)
            .HasMaxLength(128);

        builder.Property(n => n.Priority)
            .HasMaxLength(32);

        builder.Property(n => n.Issuer)
            .HasMaxLength(128);

        builder.Property(n => n.DriverName)
            .HasMaxLength(128);

        builder.Property(n => n.DriverRole)
            .HasMaxLength(64);

        builder.Property(n => n.VehicleBrand)
            .HasMaxLength(128);

        builder.Property(n => n.IsRead)
            .IsRequired();

        builder.HasOne(n => n.UserAccount)
            .WithMany()
            .HasForeignKey(n => n.UserAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => new { n.UserAccountId, n.CreatedAt });
        builder.HasIndex(n => new { n.UserAccountId, n.IsRead });
    }
}
