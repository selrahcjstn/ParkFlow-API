using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations;

public class ParkingScheduleConfiguration : IEntityTypeConfiguration<ParkingSchedule>
{
	public void Configure(EntityTypeBuilder<ParkingSchedule> entity)
	{
		entity.HasKey(e => e.Id);

		entity.Property(e => e.Id)
			.HasDefaultValueSql("gen_random_uuid()");

		entity.Property(e => e.CreatedAt)
			.HasDefaultValueSql("CURRENT_TIMESTAMP");

		entity.Property(e => e.DayOfWeek)
			.IsRequired();

		entity.Property(e => e.StartTime)
			.IsRequired();

		entity.Property(e => e.EndTime)
			.IsRequired();

		entity.HasOne(e => e.CorSubmission)
			.WithMany()
			.HasForeignKey(e => e.SubmissionId)
			.OnDelete(DeleteBehavior.Cascade);

		entity.HasIndex(e => e.SubmissionId);
	}
}
