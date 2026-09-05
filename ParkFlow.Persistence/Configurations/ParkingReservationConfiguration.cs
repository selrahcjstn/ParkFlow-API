using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations;

public class ParkingReservationConfiguration : IEntityTypeConfiguration<ParkingReservation>
{
    public void Configure(EntityTypeBuilder<ParkingReservation> builder)
    {
        builder.ToTable("ParkingReservations");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReferenceNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(r => r.ReferenceNumber)
            .IsUnique();

        builder.Property(r => r.Reason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(r => r.AdminNotes)
            .HasMaxLength(500);

        builder.HasOne(r => r.UserAccount)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.Type)
            .HasDefaultValue(Domain.Enums.ReservationType.Normal);

        builder.HasOne(r => r.Vehicle)
            .WithMany()
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
