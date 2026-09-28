using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations;

public class GuardConfiguration : IEntityTypeConfiguration<Guard>
{
	public void Configure(EntityTypeBuilder<Guard> entity)
	{
		entity.HasKey(e => e.UserProfileId);

		entity.Property(e => e.AssignedGate)
			.IsRequired();

		entity.HasOne(e => e.UserProfile)
			.WithOne(e => e.Guard)
			.HasForeignKey<Guard>(e => e.UserProfileId)
			.OnDelete(DeleteBehavior.Cascade);

		entity.HasMany(e => e.ParkingLogs)
			.WithOne(p => p.Guard)
			.HasForeignKey(p => p.GuardId)
			.IsRequired(false)
			.OnDelete(DeleteBehavior.SetNull);

		entity.HasIndex(e => e.UserProfileId)
			.IsUnique();
	}
}
