using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations;

public class PersonnelConfiguration : IEntityTypeConfiguration<Personnel>
{
	public void Configure(EntityTypeBuilder<Personnel> entity)
	{
		entity.HasKey(e => e.UserProfileId);

		entity.Property(e => e.IdCardNumber)
			.IsRequired()
			.HasMaxLength(50);

		entity.Property(e => e.Department)
			.IsRequired()
			.HasMaxLength(100);

		entity.Property(e => e.Role)
			.IsRequired()
			.HasConversion<int>()
			.HasDefaultValue(ParkFlow.Domain.Enums.Roles.UniversityStaff)
			.HasSentinel(ParkFlow.Domain.Enums.Roles.UniversityStaff);

		entity.HasOne(e => e.UserProfile)
			.WithOne(e => e.Personnel)
			.HasForeignKey<Personnel>(e => e.UserProfileId)
			.OnDelete(DeleteBehavior.Cascade);

		entity.HasIndex(e => e.UserProfileId)
			.IsUnique();

		entity.HasIndex(e => e.IdCardNumber)
			.IsUnique();
	}
}
