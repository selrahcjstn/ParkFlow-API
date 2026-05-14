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
			.WithOne()
			.HasForeignKey<Guard>(e => e.UserProfileId)
			.OnDelete(DeleteBehavior.Cascade);

		entity.HasIndex(e => e.UserProfileId)
			.IsUnique();
	}
}
