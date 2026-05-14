using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations;

public class AdminConfiguration : IEntityTypeConfiguration<Admin>
{
	public void Configure(EntityTypeBuilder<Admin> entity)
	{
		entity.HasKey(e => e.UserProfileId);

		entity.Property(e => e.RoleLevel)
			.IsRequired();

		entity.HasOne(e => e.UserProfile)
			.WithOne()
			.HasForeignKey<Admin>(e => e.UserProfileId)
			.OnDelete(DeleteBehavior.Cascade);

		entity.HasIndex(e => e.UserProfileId)
			.IsUnique();
	}
}
