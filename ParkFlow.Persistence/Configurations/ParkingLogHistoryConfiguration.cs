using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations
{
    internal class ParkingLogHistoryConfiguration : IEntityTypeConfiguration<ParkingLogHistory>
    {
        public void Configure(EntityTypeBuilder<ParkingLogHistory> builder)
        {
            builder.ToTable("ParkingLogHistories");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.ParkingLogId)
                .IsRequired();

            builder.Property(x => x.VehicleId)
                .IsRequired();

            builder.Property(x => x.GuardId)
                .IsRequired();

            builder.Property(x => x.EntryTime)
                .IsRequired();

            builder.Property(x => x.ExitTime)
                .IsRequired();

            // Optional: indexes for faster queries
            builder.HasIndex(x => x.VehicleId);
            builder.HasIndex(x => x.ParkingLogId);
        }
    }
}