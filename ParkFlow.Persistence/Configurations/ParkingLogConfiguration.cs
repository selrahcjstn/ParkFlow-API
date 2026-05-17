using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations;

public class ParkingLogConfiguration : IEntityTypeConfiguration<ParkingLog>
{
	public void Configure(EntityTypeBuilder<ParkingLog> entity)
	{
		entity.HasKey(e => e.Id);

		entity.Property(e => e.Id)
			.HasDefaultValueSql("gen_random_uuid()");

		entity.Property(e => e.CreatedAt)
			.HasDefaultValueSql("CURRENT_TIMESTAMP");

		entity.Property(e => e.VehicleId)
			.IsRequired();

		entity.Property(e => e.GuardId)
			.IsRequired();

		entity.Property(e => e.EntryTime)
			.IsRequired();

		entity.Property(e => e.ExitTime)
			.IsRequired(false);

		entity.Property(e => e.Status)
			.IsRequired();

		entity.HasOne(e => e.Vehicle)
			.WithMany(v => v.ParkingLogs)
			.HasForeignKey(e => e.VehicleId)
			.OnDelete(DeleteBehavior.Cascade);

		entity.HasOne(e => e.Guard)
			.WithMany(g => g.ParkingLogs)
			.HasForeignKey(e => e.GuardId)
			.OnDelete(DeleteBehavior.Cascade);

		entity.HasIndex(e => e.VehicleId);
		entity.HasIndex(e => e.GuardId);
	}
}
