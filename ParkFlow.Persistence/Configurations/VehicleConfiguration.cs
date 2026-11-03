using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
	public void Configure(EntityTypeBuilder<Vehicle> entity)
	{
		entity.HasKey(e => e.Id);

		entity.Property(e => e.Id)
			.HasDefaultValueSql("gen_random_uuid()");

		entity.Property(e => e.CreatedAt)
			.HasDefaultValueSql("CURRENT_TIMESTAMP");

		entity.ToTable(t => t.HasCheckConstraint(
			"CK_Vehicles_PlateNumber_Or_ElectricBike",
			"\"VehicleType\" = 1 OR (\"PlateNumber\" IS NOT NULL AND length(btrim(\"PlateNumber\")) > 0)"));

		entity.Property(e => e.PlateNumber)
			.IsRequired(false)
			.HasMaxLength(20);

		entity.Property(e => e.Brand)
			.IsRequired()
			.HasMaxLength(100);

		entity.Property(e => e.QrCodeHash)
			.IsRequired()
			.HasMaxLength(256);

		entity.Property(e => e.OrcrDocumentUrl)
			.HasMaxLength(1000);

		entity.Property(e => e.VehiclePictureUrl)
			.HasMaxLength(1000);

		entity.Property(e => e.VerificationStatus)
			.HasDefaultValue(Domain.Enums.CorVerificationStatus.Pending)
			.HasSentinel(Domain.Enums.CorVerificationStatus.Pending);

		entity.Property(e => e.RejectionReason)
			.HasMaxLength(1000);

		entity.HasOne(e => e.Owner)
			.WithMany()
			.HasForeignKey(e => e.OwnerId)
			.OnDelete(DeleteBehavior.Cascade);

		entity.HasMany(e => e.ParkingLogs)
			.WithOne(p => p.Vehicle)
			.HasForeignKey(p => p.VehicleId)
			.OnDelete(DeleteBehavior.Cascade);

		entity.HasIndex(e => e.OwnerId);
	}
}
