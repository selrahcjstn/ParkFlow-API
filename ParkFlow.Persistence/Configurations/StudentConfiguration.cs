using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ParkFlow.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
	public void Configure(EntityTypeBuilder<Student> entity)
	{
		entity.HasKey(e => e.UserProfileId);

		entity.Property(e => e.StudentNumber)
			.IsRequired()
			.HasMaxLength(50);

		entity.Property(e => e.Course)
			.IsRequired()
			.HasMaxLength(100);

		entity.Property(e => e.Section)
			.IsRequired()
			.HasMaxLength(50);

		entity.Property(e => e.YearLevel)
			.IsRequired();

		entity.HasOne(e => e.UserProfile)
			.WithOne(e => e.Student)
			.HasForeignKey<Student>(e => e.UserProfileId)
			.OnDelete(DeleteBehavior.Cascade);

		entity.HasIndex(e => e.UserProfileId)
			.IsUnique();

		entity.HasIndex(e => e.StudentNumber)
			.IsUnique();
	}
}
