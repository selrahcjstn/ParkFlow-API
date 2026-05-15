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

		entity.Property(e => e.PlateNumber)
			.IsRequired()
			.HasMaxLength(20);

		entity.Property(e => e.Brand)
			.IsRequired()
			.HasMaxLength(100);

		entity.Property(e => e.QrCodeHash)
			.IsRequired()
			.HasMaxLength(256);

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
